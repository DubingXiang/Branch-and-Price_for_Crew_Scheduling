using System;
using System.Collections.Generic;
using System.Collections;
using System.Threading;
using System.Linq;
using System.Text;
using System.IO;
using ILOG.Concert;
using ILOG.CPLEX;

namespace CG_CSP_1440
{
    class CSP
    {
        public  List<Pairing> PathSet;
        public List<double> CoefSet;//Cj
        public List<int[]> A_Matrix;//aji

        public List<INumVar> DvarSet;

        int initialPath_num;
        int trip_num;
        int realistic_trip_num;
        NetWork Network;
        List<Node> NodeSet;
        List<Node> TripList;

        #region //auxiliary slack variables        
        ArrayList ExtraCovered ;
        ArrayList Uncovered;
        double[] Penalty;
        double[] U;        
        #endregion
                
        public Cplex masterModel;
        IObjective Obj;
        IRange[] Constraint;

        RCSPP R_C_SPP;//至始至终定价问题都只有一个实体，求解过程中只是改变各属性值

        List<Pairing> ColumnPool = new List<Pairing>();

        //新添加：12-11-2018
        List<Pairing> OptColumns;
        

        //2-23-2019
        public double OBJVALUE; 

        TreeNode root_node;
        //Stack<INumVar> var_to_branch;
        List<int> best_feasible_solution; //元素值为1的为决策变量的下标。只记录一个可行解，因为分支定界过程中，始终只存在一个可行解

        //参数
        int num_AddColumn = 10;
        double GAP = 0.01;

        public CSP(NetWork Network) 
        {
            this.Network = Network;
            NodeSet = Network.NodeSet;
            TripList = Network.TripList; //TODO:测试 2-21-2019
            //TripList = new List<Node>();
            //foreach (Node trip in NodeSet)
            //{                
            //    if (trip.LineID > 0)
            //    {
            //        TripList.Add(trip);
            //    }            
            //} 
            Topological2 topo;
            R_C_SPP = new RCSPP(this.Network, out topo);
        }

        //暂时不用这个：引入了一个松弛变量和一个辅助变量
        public void Build_RMP_auxiliary(InitialSolution IS)
        {            
           // Network = IS.Net;
            //NodeSet = IS.NodeSet;            
            initialPath_num = IS.PathSet.Count;
            TripList = new List<Node>(); 

            int i, j;
            Node trip;
            for (i = 0; i < NodeSet.Count; i++)
            {
                trip = NodeSet[i];
                //trip.Visited = false;
                if (trip.ID != 0 && trip.ID != -1)
                {
                    TripList.Add(trip);
                }
            }
            trip_num = TripList.Count;
            realistic_trip_num = NetWork.num_Physical_trip;//trip_num / CrewRules.MaxDays;

            DvarSet        = new List<INumVar>();//new ArrayList();
            CoefSet    = new List<double>();
            A_Matrix = new List<int[]>();
            PathSet  = new List<Pairing>();

            ExtraCovered = new ArrayList();
            Uncovered = new ArrayList();
            Penalty = new double[realistic_trip_num];
            U = new double[realistic_trip_num];

            //IS.PrepareInputForRMP(TripList); 
            for (i = 0; i < IS.Coefs.Count; i++) {
                CoefSet.Add(IS.Coefs[i]);
            }
            for (i = 0; i < IS.A_Matrix.Count; i++) {
                A_Matrix.Add(IS.A_Matrix[i]);
            }
            for (i = 0; i < IS.PathSet.Count; i++) {
                PathSet.Add(IS.PathSet[i]);
            }                                    

            masterModel = new Cplex();
            Obj = masterModel.AddMinimize();
            Constraint = new IRange[realistic_trip_num];
            /**按行建模**/

            //slack var
            int M = 2880 * CrewRules.MaxDays;
            for (i = 0; i < realistic_trip_num; i++)
            {
                Penalty[i] = 45 * M;//铭：取36--186总覆盖次数,34交路数
                U[i] = realistic_trip_num * M;
                INumVar b = masterModel.NumVar(0, 50, NumVarType.Float);
                ExtraCovered.Add(b);
                INumVar y = masterModel.NumVar(0, 1, NumVarType.Float);
                Uncovered.Add(y);
            }
            //vars and obj function
            for (j = 0; j < initialPath_num; j++) 
            {
                INumVar var = masterModel.NumVar(0, 1, NumVarType.Float);
                DvarSet.Add(var);
                Obj.Expr = masterModel.Sum(Obj.Expr, masterModel.Prod(CoefSet[j], (INumVar)DvarSet[j]));
            }
            //add slack vars to obj function
            for (i = 0; i < realistic_trip_num; i++) 
            {
                INumExpr expr1 = masterModel.NumExpr();
                expr1 = masterModel.Sum(expr1, masterModel.Prod(Penalty[i], (INumVar)ExtraCovered[i]));
                INumExpr expr2 = masterModel.NumExpr();
                expr2 = masterModel.Sum(expr2, masterModel.Prod(U[i], (INumVar)Uncovered[i]));

                Obj.Expr = masterModel.Sum(Obj.Expr, masterModel.Sum(expr1, expr2));
            }

            //constraints
            for (i = 0; i < realistic_trip_num; i++)
            {
                INumExpr expr = masterModel.NumExpr();
                //int num_trip_cover = 0;//该trip在第j条路中覆盖的次数（实际最短路求解时，得到的单条交路中不允许一个区段多天覆盖，这里这个步骤是保险起见，先写着）
                for (j = 0; j < initialPath_num; j++)
                {
                    //for (k = 0; k < CrewRules.MaxDays; k++)
                    //{
                    //    num_trip_cover += A_Matrix[j][i + k * realistic_trip_num];
                    //}
                    expr = masterModel.Sum(expr, masterModel.Prod(A_Matrix[j][i], (INumVar)DvarSet[j]));
                }
                expr = masterModel.Sum(expr, masterModel.Prod(-1, (INumVar)ExtraCovered[i]), (INumVar)Uncovered[i]);
                //Constraint[i] = masterModel.AddGe(expr, 1);
                Constraint[i] = masterModel.AddEq(expr, 1);
            }


        }

        public void Build_RMP(InitialSolution IS)
        {            
            //NodeSet = IS.NodeSet;
            initialPath_num = IS.PathSet.Count;                       
            trip_num = TripList.Count;
            realistic_trip_num = NetWork.num_Physical_trip;

            DvarSet = new List<INumVar>();
            CoefSet = new List<double>();
            A_Matrix = new List<int[]>();
            PathSet = new List<Pairing>();

            //IS.PrepareInputForRMP(TripList);
            CoefSet = IS.Coefs;
            A_Matrix = IS.A_Matrix;
            PathSet = IS.PathSet;

            int i, j;

            #region //改前-initialSolution传递 2-21-1019
                        
            //未变的，即以非乘务时间为Cost
            //for (i = 0; i < IS.Coefs.Count; i++)
            //{
            //    Coefs.Add(IS.Coefs[i]);
            //}
            //for (i = 0; i < IS.PathSet.Count; i++) //2-21-2019
            //{
            //    PathSet.Add(IS.PathSet[i]);
            //    //Cost变为1440 or 2880
            //    Arc arc1 = PathSet[i].Arcs.First();
            //    Arc arc2 = PathSet[i].Arcs.Last();
            //    if (arc1.D_Point.ID <= realistic_trip_num && arc2.O_Point.ID > realistic_trip_num)
            //    {
            //        Coefs.Add(2880);
            //    } 
            //    else 
            //    {
            //        Coefs.Add(1440);
            //    }
            //}

            //for (i = 0; i < IS.A_Matrix.Count; i++)
            //{
            //    A_Matrix.Add(IS.A_Matrix[i]);                
            //}   
            #endregion

            masterModel = new Cplex();
            Obj = masterModel.AddMinimize();
            Constraint = new IRange[realistic_trip_num];
            /**按行建模**/
            //vars and obj function
            for (j = 0; j < initialPath_num; j++)
            {
                INumVar var = masterModel.NumVar(0, 1, NumVarType.Float);
                DvarSet.Add(var);
                Obj.Expr = masterModel.Sum(Obj.Expr, masterModel.Prod(CoefSet[j], DvarSet[j]));
            }         
            //constraints
            for (i = 0; i < realistic_trip_num; i++)
            {
                INumExpr expr = masterModel.NumExpr();
                
                for (j = 0; j < initialPath_num; j++)
                {                   
                    expr = masterModel.Sum(expr, 
                                           masterModel.Prod(A_Matrix[j][i], DvarSet[j]));//在从初始解传值给A_Matrix，已经针对网络复制作了处理
                }
                
                Constraint[i] = masterModel.AddGe(expr, 1);
            }
        }        

        public void LinearRelaxation() 
        {            
                        
            Topological2 topo;
            R_C_SPP = new RCSPP(Network, out topo);
            //RCSPP R_C_SPP = new RCSPP();              
            //R_C_SPP.UnProcessed = new List<Node>();
            //for (i = 0; i < Topo.Order.Count; i++)
            //{
            //    R_C_SPP.UnProcessed.Add(Topo.Order[i]);
            //}
            
            int iter_num = 0;
                     
            masterModel.SetParam(Cplex.IntParam.RootAlg, Cplex.Algorithm.Primal);         
            for (; ; ) 
            {
                
                if (masterModel.Solve())
                {
                    //output solution information
                    Console.WriteLine("{0},{1}", "masterProblem ObjValue: ", masterModel.GetObjValue());
                    Console.WriteLine("iteration time: " + iter_num++);

                    #region stuck test, restrict iter time
                    //if (iter_num > 200)
                    //{
                    //    int p = 0;
                    //    for (p = PathSet.Count - 1; p >= PathSet.Count - 5; p--)
                    //    {
                    //        //if (masterModel.GetReducedCost((INumVar)X[p]) != 0)
                    //        //{
                    //            Console.Write("X" + p + ": " + masterModel.GetReducedCost((INumVar)X[p]) + "\t");
                    //        //}
                    //    }
                    //    Console.WriteLine("\n//////");
                    //    Node trip;
                    //    int id;
                    //    for (p = 0; p < PathSet.Last().Arcs.Count; p++) {
                    //        Console.WriteLine("ARC COST: " + PathSet.Last().Arcs[p].Cost + ", ");
                    //    }

                    //    for ( p = 0; p < PathSet.Last().Arcs.Count - 1; p++)
                    //    {
                    //        trip = PathSet.Last().Arcs[p].D_Point;
                    //        id = trip.LineID;
                            
                    //        Console.Write(id + " dual: " + masterModel.GetDual(Constraint[id - 1]) + ",\t");
                    //    }
                    //    //break;
                    //}
                    #endregion
                    
                    Change_Arc_Length();                    

                    R_C_SPP.ShortestPath("Forward");//必须保证unprocessed里的点未处理，对Labels清空恢复初始状态
                    R_C_SPP.FindNewPath();
                    Console.WriteLine("\n子问题 objValue: " + R_C_SPP.Reduced_Cost);                    

                    if (R_C_SPP.Reduced_Cost >= -0.1)
                    {
                        break;
                    }
                    //else if (Check_Stuck(R_C_SPP.Reduced_Cost, R_C_SPP.New_Path)) 
                    //{
                    //    int yy = 0;
                    //    break;
                    //}
                    
                    //Add Column          
                    ColumnPool.Add(R_C_SPP.New_Column);

                    Pairing newPath  = R_C_SPP.New_Column;                    
                    double newCoef    = newPath.Cost;
                    int[] newAji = R_C_SPP.newAji;
                    INumVar newColumn = masterModel.NumVar(0, 1, NumVarType.Float);
                    
                    Obj.Expr = masterModel.Sum(Obj.Expr, masterModel.Prod(newCoef, newColumn));
                    for (int i = 0; i < realistic_trip_num; i++)
                    {                        
                        Constraint[i].Expr = masterModel.Sum(Constraint[i].Expr,
                                                            masterModel.Prod(newAji[i], newColumn));
                    }
                    DvarSet.Add(newColumn);
                    A_Matrix.Add(newAji);
                    CoefSet.Add(newCoef);
                    PathSet.Add(newPath);
                }
                else 
                {
                    Console.WriteLine("failure to find solution of master problem!!! ");
                    break;
                }

            }
        
        }
        void Change_Arc_Length()
        {
            int i, j;
            double price = 0;
            for (i = 0; i < realistic_trip_num; i++) {
                price = masterModel.GetDual(Constraint[i]);
                //Console.Write("dual price {0}: {1}\t", i + 1, price);
                for (j = 0; j < trip_num; j++) {                    
                    if (TripList[j].LineID == i + 1) {
                        // constraint对应的dual 与tripList的不对应，由于删去了某些点，不能以Index来查找点ID
                        //triplist Nodeset几个引用间的关系也不明确，牵涉到拓扑、最短路、原始网络
                        TripList[j].Price = price;
                    }
                }
            }
        }

        void Add_Columns(int add_type) //Enum add_type
        {
            switch (add_type) 
            {
                case 0:

                    break;
                case 1:
                    break;
                default:
                    Console.WriteLine("wrong input type");
                    Thread.Sleep(3000);
                    break;
            }
        }
              
        #region //判断是否stuck
        //bool Check_Stuck(double Reduced_Cost, LoopPath Column) 
        //{
        //    int index = ColumnPool.Count - 1;
        //    bool flag = false;
        //    //TODO:不能仅仅依据交路内容判断，这一块还是不是很清楚感觉
        //    for (int i = ColumnPool.Count - 1; i >= 0; i--) {
        //        foreach (Arc arc1 in ColumnPool[i].Arcs) {
        //            //if() {
        //        //foreach (Arc arc2 in Column.Arcs) {
        //        //        flag = arc1.Equals(arc2);
        //        //    }
        //        //}
        //        }
        //    }

        //    return flag;
        //}
        #endregion
        
        /********************/
        
        public void Branch_and_Price(InitialSolution IS) 
        {
            Build_RMP(IS);

            root_node = new TreeNode();
            CG(root_node);

            best_feasible_solution = new List<int>();                        
            RecordFeasibleSolution(root_node, ref best_feasible_solution);

            double UB = IS.GetObjValue();//int.MaxValue;
            double LB = root_node.obj_value;

            Branch_and_Bound(root_node, LB, UB);

        
        }

        #region 分支定界
        
        public void Branch_and_Bound(TreeNode root_node, double LB, double UB) 
        {            
            while (true)
            {
                if (TerminationCondition(root_node) == true)
                    break;

                #region //先判断可行，再比较目标函数大小
                /*
                if (Feasible(root_node) == false)
                {
                    if (root_node.obj_value > UB) //不必在该点继续分支
                    {
                        Backtrack(ref root_node.fixed_vars);                                              
                    }
                    else //root_node.obj_value <= UB,有希望，更新下界，继续分支,
                    {
                        LB = root_node.obj_value;                        
                    }   
                }
                else //可行，更新上界。【2-24-2019：只要可行，不管可行解是否优于当前最优可行（OBJ < UB）都不用在该点继续分支，而是回溯】
                {                    
                    if (root_node.obj_value <= UB) //root_node.obj_value <= UB，更新UB，停止在该点分支，回溯
                    {
                        UB = root_node.obj_value;
                        RecordFeasibleSolution(root_node, ref best_feasible_solution);

                        if ((UB - LB) / UB < GAP) //这也是停止准则，但只能放在这里
                        {                            
                            break;
                        }
                    }

                    Backtrack(ref root_node.fixed_vars); 
                }

                SolveChildNode(ref root_node);
                */
                #endregion

                #region 先比较界限，再判断是否可行
                if (root_node.obj_value > UB) //不论可行与否，只要大于上界，都必须剪枝，然后回溯
                {                    
                    Backtrack(ref root_node.fixed_vars);                    
                }
                else if (LB <= root_node.obj_value && root_node.obj_value <= UB) 
                {
                    if (Feasible(root_node) == false) //不可行，更新下界；继续向下分支
                    {
                        LB = root_node.obj_value;
                    }
                    else //可行，更新上界，记录当前可行解；回溯
                    {
                        UB = root_node.obj_value;
                        RecordFeasibleSolution(root_node, ref best_feasible_solution);
                        Backtrack(ref root_node.fixed_vars);
                    }
                }

                Branch(ref root_node); //寻找需要分支的变量

                CG(root_node); //求解子节点

                #endregion
            }
        }

        bool TerminationCondition(TreeNode node) 
        {

            return NoVarBranchable(node);
        }
        bool NoVarBranchable(TreeNode node)
        {
            /*没有节点（变量）可以回溯，所有变量分支过了
                           * 没有变量可以分支，所有变量都分支过了
                            */
            if (root_node.fixed_vars.Count == 0
                || root_node.not_fixed_var_value_pairs.Count == 0)
            {
                Console.WriteLine("找不到可分支的变量");
                return true;
            }
            return false;
        }

        bool Feasible(TreeNode node) 
        {            
            foreach (var var_value in node.not_fixed_var_value_pairs) 
            {
                if (ISInteger(var_value.Value) == false) 
                {
                    return false;
                }    
            }
            return true;
        }
        bool ISInteger(double value, double epsilon = 1e-12) //本问题是0-1变量，这里为了普遍性，判断整数
        {
            return Math.Abs(value - Convert.ToInt32(value)) <= epsilon; //不是整数，不可行（浮点型不用 == ，!=比较）           
        }
        
        void Backtrack(ref List<int> fixed_vars) 
        {                        
            fixed_vars.RemoveAt(fixed_vars.Count - 1);
        }
        void Branch(ref TreeNode node)
        {
            //找字典最大Value对应的key
            /*用lambda： var k = node.not_fixed_var_value_pairs.FirstOrDefault(v => v.Value.Equals
                                                                            *(node.not_fixed_var_value_pairs.Values.Max()));*/

            int var_of_maxvalue = node.not_fixed_var_value_pairs.First().Key;
            double maxvalue = node.not_fixed_var_value_pairs.First().Value;

            foreach (var var_value in node.not_fixed_var_value_pairs) 
            {
                var_of_maxvalue = maxvalue > var_value.Value ? var_of_maxvalue : var_value.Key;
            }

            node.fixed_vars.Add(var_of_maxvalue);
            node.not_fixed_var_value_pairs.Remove(var_of_maxvalue);
        }

        void RecordFeasibleSolution(TreeNode node, ref List<int> best_feasible_solution)//TODO:因为fixed_vars被fixed为1，所以只需记录值为分数的var（0 or 1），即待分支的var 2-23-2019
        {
            best_feasible_solution.Clear(); //找到更好的可行解了，不需要之前的了

            foreach (var var_value in node.not_fixed_var_value_pairs) 
            {
                if (Convert.ToInt32(var_value.Value) > 0 && ISInteger(var_value.Value)) 
                {
                    best_feasible_solution.Add(var_value.Key);
                }
            }
        }

        public void SolveChildNode(ref TreeNode node) //没必要合并，对吧
        {                        
            Branch(ref root_node);
            CG(root_node);           
        }
        
        #endregion

        #region 列生成
         /*******CG相关*******/
        public void CG(TreeNode tree_node)
        {
            //在CG之前，RMP已经构造完毕，即BuildRMP已执行
            //固定分支变量的值（==1）等于是另外加上变量的取值范围约束
            FixVars(tree_node.fixed_vars);

            int result = SolveRMP(); // 0-最优            
                       
            //生成列
            for (; ; )
            {
                if (IsLPOpt())
                {
                    break;
                }
                    
                Console.WriteLine("检验数(min)： " + R_C_SPP.Reduced_Cost);
                //TODO:add columns
                double col_coef = 0;
                int[] aj;
                foreach (Pairing column in R_C_SPP.New_Columns) 
                {
                    ColumnPool.Add(column);

                    col_coef = column.Coef;
                    aj = column.CoverMatrix;

                    INumVar column_var = masterModel.NumVar(0, 1, NumVarType.Float);
                    // function
                    Obj.Expr = masterModel.Sum(Obj.Expr, masterModel.Prod(col_coef, column_var));
                    // constrains
                    for (int i = 0; i < realistic_trip_num; i++)
                    {
                        Constraint[i].Expr = masterModel.Sum(Constraint[i].Expr,
                                                            masterModel.Prod(aj[i], column_var));
                    }

                    DvarSet.Add(column_var);
                    A_Matrix.Add(aj);
                    CoefSet.Add(col_coef);                    
                }                  
            }
        }
        void FixVars(List<int> fixed_vars) 
        {
            foreach (int i in fixed_vars) 
            {
                DvarSet[i].LB = 1.0;
                DvarSet[i].UB = 1.0;
            }
        }

        public int SolveRMP()
        {
            int status = 0;
            try
            {
                if (masterModel.Solve())
                {
                    Console.WriteLine("{0}: {1}", "Current RMP ObjValue", masterModel.GetObjValue());
                }
                else
                {
                    throw new ILOG.Concert.Exception();
                }
            }
            catch (ILOG.Concert.Exception ex)
            {
                Console.WriteLine("Current RMP can't solved, there might exit some error");
                Console.WriteLine("{0} from {1}", ex.Message, ex.Source);
            }
            return status;
        }
        
        bool IsLPOpt() //求解子问题，判断 检验数 < 0 ?
        {
            Change_Arc_Length();
            this.R_C_SPP.ShortestPath("Forward");

            //FindMultipleNewPColumn():True-找到新列，继续迭代;False-找不到新列，最优,停止
            bool opt = !this.R_C_SPP.FindMultipleNewPColumn(num_AddColumn) || R_C_SPP.Reduced_Cost > -1e12;
            return opt;
        }

        #endregion
        
        

    }
        
        

}
