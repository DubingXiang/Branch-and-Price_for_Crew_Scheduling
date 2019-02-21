using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;

namespace CG_CSP_1440
{
    class InitialSolution
    {
        //输出,传的应该是 out来传
        public List<int[]> A_Matrix;
        public List<double> Coefs;
        public List<Pairing> PathSet;

        //输入变量
        NetWork Net;
        List<Node> NodeSet;
        //List<Node> TripList;//目标点集。依次以tripList中的点为起点，求其顺逆向寻最短路
        List<Node> LineList;

        public InitialSolution(NetWork Network) 
        {
            Net = Network;
            NodeSet = Net.NodeSet;
            LineList = new List<Node>();
            PathSet = new List<Pairing>();
            //TripList = Net.TripList;            
            for (int i = 0; i < Net.TripList.Count/CrewRules.MaxDays; i++)
            {
                LineList.Add(Net.TripList[i]);                
            }
        }

        public List<Pairing> GetFeasibleSolutionByMethod1()
        {         
            //中间变量，用来传值
            Node trip = new Node();
            Pairing loopPath;
            int i, j;                        
            Node s = NodeSet[0];
            Topological2 Topo = new Topological2(Net, s);

            RCSPP R_C_SPP = new RCSPP(Topo.Order);           
            //R_C_SPP.UnProcessed = new List<Node>();
            //for (i = 0; i < Topo.Order.Count; i++) {
            //    R_C_SPP.UnProcessed.Add(Topo.Order[i]);
            //}
            R_C_SPP.ShortestPath("Forward");
            
            R_C_SPP.UnProcessed = Topo.Order;
            R_C_SPP.ShortestPath("Backward");
            //for (i = 0; i < NodeSet.Count; i++) 
            //{
            //    trip = NodeSet[i];
            //    trip.Visited = false;
            //    if (trip.ID != 0 && trip.ID != -1) {
            //        TripList.Add(trip);
            //    }
            //}
            //也按拓扑顺序否？？
            while (LineList.Count > 0) 
            {
                trip = LineList[0];//这里以 1,2,3...顺序寻路，使得许多路的大部分内容相同，可不可以改进策略                
                loopPath = FindFeasiblePairings(trip);
                LineList.RemoveAt(0);
                if (loopPath.Arcs == null)
                {
                    throw new Exception("找不到可行回路！咋办啊！！");
                }
                else 
                {
                    PathSet.Add(loopPath);
                    for (i = 0; i < loopPath.Arcs.Count; i++) 
                    {
                        trip = loopPath.Arcs[i].D_Point;
                        for (j = 0; j < LineList.Count; j++) 
                        {
                            if (LineList[j].ID == trip.ID) 
                            {
                                //trip.Visited = true;
                                LineList.RemoveAt(j); 
                                break; 
                            }
                        }
                    }
                }                
            }

            PrepareInputForRMP(Net.TripList);

            return this.PathSet;
        }
        Pairing FindFeasiblePairings(Node trip)
        {
            Pairing loopPath = new Pairing();//output
            loopPath.Arcs = new List<Arc>();
            int i,j;
            int minF = 0, minB = 0;
            Label labelF, labelB;
            Arc arc;            

            Double AccumuDrive, T3, C;
            double MAX = 666666;
            Double Coef = MAX;
            for (i = 0; i < trip.LabelsForward.Count; i++) 
            {
                labelF = trip.LabelsForward[i];                
                for (j = 0; j < trip.LabelsBackward.Count; j++) 
                {
                    labelB = trip.LabelsBackward[j];

                    AccumuDrive = labelF.AccumuDrive + labelB.AccumuDrive - trip.Length;
                    T3 = labelF.AccumuWork + labelB.AccumuWork - trip.Length;
                    C = labelF.AccumuCost + labelB.AccumuCost - trip.Length;
                    //求初始解时，Cost即为非乘务时间，即目标函数，而在列生成迭代中，非也（因为对偶乘子）                  
                    if (AccumuDrive   <= CrewRules.PureCrewTime &&
                        T3   <= CrewRules.TotalCrewTime&&
                        Coef >= C) //find minmal cost
                    {
                        minF = i;
                        minB = j;
                        Coef = C;
                        
                    }
                }
            }
            if (Coef < MAX) 
            {
                labelF = trip.LabelsForward[minF];
                labelB = trip.LabelsBackward[minB];
                loopPath.Cost = Coef;
                arc = labelF.PreEdge;
                while (arc.O_Point.ID != 0)
                {
                    loopPath.Arcs.Insert(0, arc);
                    labelF = labelF.PreLabel;
                    arc = labelF.PreEdge;
                }
                loopPath.Arcs.Insert(0, arc);

                arc = labelB.PreEdge;
                while (arc.D_Point.ID != 1)
                {
                    loopPath.Arcs.Add(arc);
                    labelB = labelB.PreLabel;
                    arc = labelB.PreEdge;
                }
                loopPath.Arcs.Add(arc);
            }
            if (loopPath.Arcs.Count == 0)
            {                                  
                loopPath = default(Pairing);
            } 
            return loopPath; 
        }

        public List<Pairing> GetFeasibleSolutionByPenalty() 
        {
            Node trip = new Node();
            Pairing Pairing;
            int i;
            Node s = NodeSet[0];
            Topological2 Topo = new Topological2(Net, s);

            RCSPP R_C_SPP = new RCSPP(Topo.Order);
            //R_C_SPP.UnProcessed = new List<Node>();
            //for (i = 0; i < Topo.Order.Count; i++)
            //{
            //    R_C_SPP.UnProcessed.Add(Topo.Order[i]);
            //}
            int M = 99999;
            R_C_SPP.ChooseCostDefinition(0);
            Arc arc;
           //迭代，直到所有trip被cover
            while (LineList.Count > 0) 
            {                
                R_C_SPP.ShortestPath("Forward");
                
                R_C_SPP.FindNewPath();
                Pairing = R_C_SPP.New_Column;
                PathSet.Add(Pairing);
                for (i = 1; i < Pairing.Arcs.Count - 2; i++) //起终点不用算
                {
                    arc = Pairing.Arcs[i];
                    arc.D_Point.numVisited++; 
                    arc.D_Point.Price = -arc.D_Point.numVisited * M;
                    LineList.Remove(arc.D_Point);
                }
            }

            PrepareInputForRMP(Net.TripList);

            return this.PathSet;
        }
        
        private void PrepareInputForRMP(List<Node> TripList) //2-21-2019改前是 ref
        {
            //Get Coef in FindAllPaths
            Coefs = new List<double>();
            A_Matrix = new List<int[]>();
            int realistic_trip_num = NetWork.num_Physical_trip;
            foreach (var path in PathSet) {
                //Coefs.Add(path.Cost);
                Coefs.Add(path.Coef); //TODO:待测试 2-21-2019

                int[] a = new int[realistic_trip_num];
                for (int i = 0; i < realistic_trip_num; i++) {
                    a[i] = 0;
                }

                foreach (Node trip in TripList) {
                    foreach (Arc arc in path.Arcs) {
                        if (arc.D_Point == trip) {                            
                            a[trip.LineID - 1] = 1;
                        }
                    }
                }

                A_Matrix.Add(a);
            }
        }

    }
    
    //资源约束最短路
    class RCSPP 
    {                
        //OUTPUT
        public  double Reduced_Cost;
        public  Pairing New_Column;
        //public int[] newAji;
        //public int[,] newMultiAji;
        //添加的
        public List<Pairing> New_Columns;
        List<Label> negetiveLabels;
        //public double[] reduced_costs;

        //与外界相关联的
        public List<Node> UnProcessed = new List<Node>();//Topo序列
        public List<CrewBase> CrewbaseList = DataFromSQL.CrewBaseList; //2-20-2019

        double accumuConsecDrive, accumuDrive, accumuWork, C;//资源向量,cost
        bool resource_feasible;
        string direction;

        //public Dictionary<string, int> costDefinition = new Dictionary<string, int>();
        int costType;
        
        public RCSPP(List<Node> topological_Order) 
        {
            foreach (Node node in topological_Order) 
            {
                UnProcessed.Add(node);
            }
        }

        /// <summary>
        /// 0<= definetype <=2,选择成本的具体定义。//（此前可定义一个字典，选择定义）
        /// </summary>
        /// <param name="defineType"></param>
        public void ChooseCostDefinition(int defineType)
        {
            try
            {
                if (0 <= defineType && defineType <= 2) { this.costType = defineType; }
                else { throw new System.Exception("参数输入错误, defineType must in [0, 2]"); }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public void ShortestPath(string Direction) //Forward;Backward
        {
            direction = Direction;
            Node trip1, trip2;            
            Label label1, label2;
            int i, j;

            InitializeStartNode();
            //framework of labeling setting algorithm                                                
            if (direction == "Forward")
            {
                for (int t = 0; t < UnProcessed.Count; t++)
                {
                    trip1 = UnProcessed[t];
                    //2-20-2019                       
                    if (trip1.Type == 0) //基地起点
                    {
                        InitializeBaseNode(trip1, trip1.LabelsForward);                        
                    }
                    //end
                    //dominated rule
                    //这里可以再想想优化,实际上类似于排序，采用类似于归并排序的处理,本质是分治思想
                    #region  未优化的，直接两两比较，复杂度 O(n^2)(实际共比较n(n-1)/2次)
                    //for (int l1 = 0; l1 < trip1.LabelsForward.Count; l1++)
                    //{
                    //    for (int l2 = l1 + 1; l2 < trip1.LabelsForward.Count; l2++)
                    //    {
                    //        DominateRule(trip1.LabelsForward[l1], trip1.LabelsForward[l2]);
                    //    }
                    //}
                    //for (i = 0; i < trip1.LabelsForward.Count; i++)
                    //{
                    //    if (trip1.LabelsForward[i].Dominated == true)
                    //    {
                    //        trip1.LabelsForward.RemoveAt(i);
                    //        i--;
                    //    }
                    //}
                    #endregion
                    //优化后，速度提高了10多倍
                    RemainPateroLabels(ref trip1.LabelsForward);

                    //判断是否可延伸，即是否 resource-feasible
                    for (i = 0; i < trip1.Out_Edges.Count; i++)
                    {
                        Arc arc = trip1.Out_Edges[i];
                        trip2 = arc.D_Point;
                        
                        for (j = 0; j < trip1.LabelsForward.Count; j++)
                        {                            
                            label1 = trip1.LabelsForward[j];

                            resource_feasible = false;
                            label2 = REF(label1, trip2, arc);
                            if (resource_feasible)//label1可延伸至trip2
                            {                         
                                trip2.LabelsForward.Add(label2);
                            }
                            else { label2 = default(Label); }
                        }
                    }
                }
            }
            else if (direction == "Backward")
            {
                while (UnProcessed.Count > 0)
                {
                    trip1 = UnProcessed[0];

                    if (trip1.Type == 2) //基地起点
                    {
                        InitializeBaseNode(trip1, trip1.LabelsBackward);
                    }

                    UnProcessed.Remove(trip1);
                    //dominated rule
                    RemainPateroLabels(ref trip1.LabelsBackward);
                    //判断是否可延伸，即是否 resource-feasible
                    for (i = 0; i < trip1.In_Edges.Count; i++)
                    {
                        Arc arc = trip1.In_Edges[i];
                        trip2 = arc.O_Point;
                        for (j = 0; j < trip1.LabelsBackward.Count; j++)
                        {                            
                            label1 = trip1.LabelsBackward[j];

                            resource_feasible = false;
                            label2 = REF(label1, trip2, arc);
                            if (resource_feasible)//label1可延伸至trip2
                            {                                                           
                                trip2.LabelsBackward.Add(label2);
                            }
                            else { label2 = default(Label); }
                        }
                    }
                }
            }               
        }
        void InitializeStartNode() 
        {
            Label oLabel = new Label();
            if (direction == "Forward")
            {                
                UnProcessed[0].LabelsForward.Add(oLabel);
                //initailize(clean) labels of all trips
                for (int i = 1; i < UnProcessed.Count; i++)
                {
                    UnProcessed[i].LabelsForward.Clear();                    
                }
            }
            if (direction == "Backward")
            {
                UnProcessed.Reverse();
                //UnProcessed[0].LabelsBackward[0].AccumuConsecDrive = 0;
                //UnProcessed[0].LabelsBackward[0].AccumuDrive = 0;
                //UnProcessed[0].LabelsBackward[0].AccumuWork = 0;
                //UnProcessed[0].LabelsBackward[0].AccumuCost = 0;
                //UnProcessed[0].LabelsBackward[0].PreEdge = new Arc();
                UnProcessed[0].LabelsBackward.Add(oLabel);
                //initailize(clean) labels of all trips
                for (int i = 1; i < UnProcessed.Count; i++)
                {
                    UnProcessed[i].LabelsBackward.Clear();
                }
            }    
        }
        void InitializeBaseNode(Node trip, List<Label> label_list) 
        {
            foreach (Label label in label_list) 
            {
                foreach(CrewBase crewbase in CrewbaseList)
                {
                    if (trip.StartStation == crewbase.Station)
                    {
                        label.BaseOfCurrentPath = crewbase;
                        break;
                    }
                }
            }
            
        }
        
        Label REF(Label label, Node trip, Arc arc) //在虚拟起终点弧,顺逆向有差别，接续弧是相同的处理
        {                   
            //首先判断，避免出现 non-elementary label(path),除了基地，其余点不允许在同一Label（path）出现两次
            if (!FitNetworkConstraints(label, trip))
            {
                resource_feasible = false;
                return default(Label);                
            }            
            //bool connected = true;//是否连接成功
            Label extend = new Label();
            #region  跟据弧的类型计算Label的各项属性值            
            if (arc.ArcType == 1)
            {
                accumuConsecDrive = label.AccumuConsecDrive + arc.Cost + trip.Length;
                if (accumuConsecDrive >= (double)CrewRules.ConsecuDrive.min) //需要间休
                {
                    if (!((int)CrewRules.Interval.min <= arc.Cost && arc.Cost <= (int)CrewRules.Interval.max))//只有这种情况连接失败：需要间休，但间休时间不满足条件
                    {
                        //connected = false;                        
                        return default(Label); //TODO：提前结束。必须测试！！
                    }
                    else 
                    {                        
                        accumuConsecDrive = trip.Length;
                        accumuDrive = label.AccumuDrive + trip.Length;
                        accumuWork = label.AccumuWork + arc.Cost + trip.Length;
                        //C  = label.AccumuCost + arc.Cost - trip.Price;
                    }
                }
                else
                {
                    accumuConsecDrive = label.AccumuConsecDrive + arc.Cost + trip.Length;
                    accumuDrive = label.AccumuDrive + arc.Cost + trip.Length;
                    accumuWork = label.AccumuWork + arc.Cost + trip.Length;
                    //C  = label.AccumuCost + arc.Cost - trip.Price;
                }
            }
            else if (arc.ArcType == 22) //跨天弧，先不和“out”弧合并
            {
                accumuConsecDrive = trip.Length;
                accumuDrive = trip.Length;
                accumuWork = trip.Length;
                //C  = label.AccumuCost + arc.Cost - trip.Price;                                                                            
            }
            else if (arc.ArcType == 20) 
            {
                accumuConsecDrive = trip.Length;
                accumuDrive = trip.Length;
                accumuWork = trip.Length;
            }
            else if (arc.ArcType == 30) 
            {
                accumuConsecDrive = label.AccumuConsecDrive;
                accumuDrive = label.AccumuDrive;
                accumuWork = label.AccumuWork;
            }
            //出退乘
            string taskType = "";
            if ((arc.ArcType == 2 && direction == "Forward") || (arc.ArcType == 3 && direction == "Backward")) { taskType = "out"; }
            if ((arc.ArcType == 3 && direction == "Forward") || (arc.ArcType == 2 && direction == "Backward")) { taskType = "back"; }
            switch (taskType)
            {
                case "out":
                    accumuConsecDrive = trip.Length;
                    accumuDrive       = trip.Length;
                    accumuWork        = trip.Length;
                    //C  = label.AccumuCost + arc.Cost - trip.Price; 
                    break;
                case "back":
                    accumuConsecDrive = label.AccumuConsecDrive;
                    accumuDrive = label.AccumuDrive;
                    accumuWork = label.AccumuWork;
                    //C  = label.AccumuCost + arc.Cost - trip.Price;
                    break;
                default: taskType = "Exception";
                    break;
            }
           
            SetCostDefinition(label, trip, arc);
            C -= trip.Price;
            //TODO:overed 成本最好还是抽离出去，可以对其进行多种不同的定义，比较哪种优
            #endregion
            //乘务规则
            if (!(accumuDrive <= CrewRules.PureCrewTime && accumuWork <= CrewRules.TotalCrewTime)) //TODO:是否满足资源约束（还要增添几个乘务规则）
            {
                resource_feasible = false;
            }
            else 
            {
                resource_feasible = true;
                extend.AccumuConsecDrive = accumuConsecDrive;
                extend.AccumuDrive = accumuDrive;
                extend.AccumuWork = accumuWork;
                extend.AccumuCost = C;
                extend.PreEdge = arc;
                extend.PreLabel = label;
                extend.BaseOfCurrentPath = label.BaseOfCurrentPath;
                //TODO:新属性
                //以索引来标记trip，可能又会有bug
                if (trip.LineID != 0)
                {
                    label.VisitedCount.CopyTo(extend.VisitedCount, 0);
                    ++extend.VisitedCount[trip.LineID - 1];
                }
            }
                                                            
            return extend;
        }
        bool FitNetworkConstraints(Label label, Node trip) 
        {
            if (trip.LineID != 0 && label.VisitedCount[trip.LineID - 1] >= 1) 
            {
                return false; //只能访问一次，elementary path
            }
            if (direction == "Forward") 
            {
                if (trip.Type == 2 && label.BaseOfCurrentPath.Station != trip.EndStation)
                {
                    return false; //path的起终基地一致
                }
            }
            else if (direction == "Backward") 
            {
                if (trip.Type == 0 && label.BaseOfCurrentPath.Station != trip.StartStation)
                {
                    return false;
                }
            }

            return true;
        }
        void SetCostDefinition(Label label, Node trip, Arc arc) //TODO
        {
            switch (costType) 
            {
                case 0://求初始解时，用非乘务时间
                    C = label.AccumuCost + arc.Cost;
                    break;
                case 1://全部时间，实际上就是trip的到达时刻。迭代过程中会减去trip.Price，
                    C = label.AccumuCost + arc.Cost + trip.Length;
                    break;
                default:
                    break;
            }
        }       

        void RemainPateroLabels(ref List<Label> labelSet) 
        {
            
            int width = 0;
            int size = labelSet.Count;
            int index = 0;
            int first, last, mid;
            for (width = 1; width < size; width *= 2) 
            {
                for (index = 0; index < (size - width); index += width * 2) 
                {
                    first = index;
                    mid = index + width - 1;
                    //last = index + (width * 2 - 1);//两组相比较，所以是width * 2
                    //last = last >= size ? size - 1 : last;
                    last = Math.Min(index + (width * 2 - 1), size - 1);
                    CheckDominate(ref labelSet, first, mid, last);
                }
            }
            //delete labels which was dominated 
            for (index = 0; index < labelSet.Count; index++)
            {
                if (labelSet[index].Dominated == true)
                {
                    labelSet.RemoveAt(index);
                    index--;
                }
            }

        }
        void CheckDominate(ref List<Label> labelSet, int first, int mid, int last) 
        {
            int i, j;
            for (i = first; i <= mid; i++) 
            {
                if (labelSet[i].Dominated) {
                    //labelSet.RemoveAt(i); i--;//先不删，反正最后统一删；现在删了反而不好处理
                    continue;
                }
                for (j = mid + 1; j <= last; j++)
                {
                    if (labelSet[j].Dominated) {
                        continue;
                    }
                    DominateRule(labelSet[i], labelSet[j]);
                }
            }
        }
        void DominateRule(Label label1, Label label2)
        {
            if (label1.AccumuCost <= label2.AccumuCost &&
                label1.AccumuConsecDrive <= label2.AccumuConsecDrive &&
                label1.AccumuDrive <= label2.AccumuDrive &&
                label1.AccumuWork <= label2.AccumuWork)
            {
                label2.Dominated = true;
            }
            else if (label2.AccumuCost <= label1.AccumuCost &&
                label2.AccumuConsecDrive <= label1.AccumuConsecDrive &&
                label2.AccumuDrive <= label1.AccumuDrive &&
                label2.AccumuWork <= label1.AccumuWork)
            {
                label1.Dominated = true;
            }
        }

        public void FindNewPath()
        {
            List<Node> topoNodeList = UnProcessed;
            Node virD = topoNodeList.Last(); //终点的确定是否可以更加普适（少以Index为索引）
            Label label1;  
            Label label2;
            int i;
            //找标号Cost属性值最小的，改变弧长后，Cost即为reduced cost,
            //而主问题的Cj为 reduced cost + sum(trip.price),
            //但在迭代过程中Cj=1440*days
            #region //可利用Linq查询
            //label1 = virD.LabelsForward.Aggregate((l1, l2) => l1.AccumuCost < l2.AccumuCost ? l1 : l2);
            
            //label1 = (from l in virD.LabelsForward
            //          let minCost = virD.LabelsForward.Max(m => m.AccumuCost)
            //          where l.AccumuCost == minCost
            //          select l).FirstOrDefault();
            #endregion
            label1 = virD.LabelsForward[0];
            for (i = 1; i < virD.LabelsForward.Count; i++) 
            {                
                //想当然了！！常规来吧，别想着骚
                //label1 = virD.LabelsForward[i - 1].AccumuCost < virD.LabelsForward[i].AccumuCost ? 
                //         virD.LabelsForward[i - 1] : virD.LabelsForward[i];
                label2 = virD.LabelsForward[i];
                if (label1.AccumuCost > label2.AccumuCost) 
                {
                    label1 = label2;
                }
            }
            
            //Reduced_Cost           = label1.AccumuCost; //2019-1-27
            New_Column               = new Pairing();
            New_Column.Arcs          = new List<Arc>();            
            
            int realistic_trip_num = NetWork.num_Physical_trip;//(nodeList.Count - 2) / CrewRules.MaxDays;
            //newAji                 = new int[realistic_trip_num];            
            //for (i = 0; i < realistic_trip_num; i++) { newAji[i] = 0; }
            New_Column.CoverMatrix = new int[realistic_trip_num];
            New_Column.Cost = label1.AccumuCost; //2019-2-1
            New_Column.Coef = 1440;
            //double sum_tripPrice   = 0;
            int pathday = 1;
            Node virO = topoNodeList[0];
            Arc arc;
            arc = label1.PreEdge;
            while (!arc.O_Point.Equals(virO)) 
            {                
                New_Column.Arcs.Insert(0, arc);
                //sum_tripPrice += arc.O_Point.Price;
                //newAji[arc.O_Point.LineID - 1] = 1;
                if (arc.O_Point.LineID > 0) 
                {
                    New_Column.CoverMatrix[arc.O_Point.LineID - 1] = 1;//虚拟起终点弧的处理
                }                
                label1 = label1.PreLabel;
                arc = label1.PreEdge;
                pathday = arc.ArcType == 22 ? pathday + 1 : pathday;
            }
            New_Column.Arcs.Insert(0, arc);
            //New_Path.Cost = Reduced_Cost + sum_tripPrice;            
            New_Column.Coef *= pathday;
        }
        //TODO:添加多列
        public bool FindMultipleNewPath(int num_addColumns)
        {
            List<Node> topoNodeList = UnProcessed;
            Node virD = topoNodeList.Last();            
            negetiveLabels = new List<Label>();

            Label label1;           
            int i;
            //找标号Cost < 0即可           
            for (i = virD.LabelsForward.Count - 1; i >= 0 ; i--)
            {
                label1 = virD.LabelsForward[i];                
                if (label1.AccumuCost < 0) 
                {
                    negetiveLabels.Add(label1);
                }
            }

            if (negetiveLabels.Count == 0) //检验数均大于0，原问题最优
            {   
                return true; 
            } 
            else
            {                             
                num_addColumns = Math.Min(num_addColumns, negetiveLabels.Count);
                //TODO:TopN排序，只想最多添加N列，则只需找出TopN即可   
                //先调用方法全部排序吧
                negetiveLabels.OrderBy(labelCost => labelCost.AccumuCost);

                Reduced_Cost = negetiveLabels[0].AccumuCost;//固定为最小Cost
                //reduced_costs = new double[num_addColumns];
                New_Columns = new List<Pairing>(num_addColumns);
                
                //局部变量                
                int realistic_trip_num = NetWork.num_Physical_trip;
                Node virO = topoNodeList[0];
                Arc arc;
                //newMultiAji = new int[num_addColumns, realistic_trip_num];//全部元素默认为0                

                for (i = 0; i < num_addColumns; i++)
                {
                    label1 = negetiveLabels[i];                   
                    //reduced_costs[i] = label1.AccumuCost;

                    New_Column             = new Pairing();
                    New_Column.Arcs        = new List<Arc>();
                    New_Column.CoverMatrix = new int[realistic_trip_num];
                    New_Column.Cost        = label1.AccumuCost;
                    New_Column.Coef        = 1440;
                    int pathday = 1;

                    arc = label1.PreEdge;
                    while (!arc.O_Point.Equals(virO))
                    {
                        New_Column.Arcs.Insert(0, arc);
                        
                        if (arc.O_Point.LineID > 0) 
                        {
                            New_Column.CoverMatrix[arc.O_Point.LineID - 1] = 1;
                        }

                        pathday = arc.ArcType == 22 ? pathday + 1 : pathday;

                        label1 = label1.PreLabel;
                        arc = label1.PreEdge;
                    }
                    New_Column.Arcs.Insert(0, arc);
                    New_Column.Coef *= pathday;

                    New_Columns.Add(New_Column);
                }                

                return false;
            }            
        }
    }

    //调试完毕，没毛病
    public class Topological2
    {
        private Queue<Node> queue;
        //private int[] Indegree;
        private Dictionary<Node, int> Indegree;         

        public List<Node> Order;//拓扑序列

        /// <summary>
        /// Network , strat point
        /// </summary>
        /// <param name="net"></param>
        /// <param name="s"></param>
        public Topological2(NetWork net, Node s)
        {
            List<Node> nodeset = net.NodeSet;
            Node trip;
            queue = new Queue<Node>();
            //Indegree = new int[net.NodeSet.Count - 1];
            Indegree = new Dictionary<Node, int>();

            Order = new List<Node>();
            for (int i = 0; i < nodeset.Count; i++) 
            {
                trip = nodeset[i];
                Indegree[trip] = trip.In_Edges.Count; 
            }
            //Indegree[Indegree.Length - 1] = net.NodeSet[Indegree.Length - 1].In_Edges.Count;
            //for (int v = 0; v < net.NodeSet.Count; v++)
            //{
            //    foreach (var a in net.ArcSet)
            //    {
            //        if (a.D_Point.ID == net.NodeSet[v].ID)
            //        {
            //            Indegree[v]++;
            //        }
            //    }
            //}
            foreach (Node node in nodeset) {
                if (node.In_Edges.Count == 0) {
                    queue.Enqueue(node);
                }
            }
            
            int count = 0;
            while (queue.Count != 0)
            {
                Node Top = queue.Dequeue();
                int top = Top.ID; 
                Order.Add(Top);               
                foreach (var arc in net.ArcSet)
                {
                    if (arc.O_Point == Top && arc.D_Point.ID != 1) //终点在最后
                    {
                        if (--Indegree[arc.D_Point] == 0)
                        {
                            queue.Enqueue(arc.D_Point);
                        }
                    }
                }
                count++;
            }
            Order.Add(nodeset[1]);//终点
            count++;
            if (count != nodeset.Count) { throw new Exception("此图有环"); }

        }
    }
}
