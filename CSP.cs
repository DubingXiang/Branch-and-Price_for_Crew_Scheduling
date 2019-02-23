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
        public List<double> Coefs;//Cj
        public List<int[]> A_Matrix;//aji

        public List<INumVar> X;

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

        List<Pairing> ColumnPool = new List<Pairing>();

        //新添加：12-11-2018
        List<Pairing> OptColumn;
        Dictionary<int, double> Value_Column;//每个var对应的解值

        //2-23-2019
        public double OBJVALUE; 

        TreeNode root_node;
        Stack<INumVar> var_to_branch;

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
        }

        //暂时不用这个：引入了一个松弛变量和一个辅助变量
        public void Build_RMP(InitialSolution IS)
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

            X        = new List<INumVar>();//new ArrayList();
            Coefs    = new List<double>();
            A_Matrix = new List<int[]>();
            PathSet  = new List<Pairing>();

            ExtraCovered = new ArrayList();
            Uncovered = new ArrayList();
            Penalty = new double[realistic_trip_num];
            U = new double[realistic_trip_num];

            //IS.PrepareInputForRMP(TripList); 
            for (i = 0; i < IS.Coefs.Count; i++) {
                Coefs.Add(IS.Coefs[i]);
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
                X.Add(var);
                Obj.Expr = masterModel.Sum(Obj.Expr, masterModel.Prod(Coefs[j], (INumVar)X[j]));
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
                    expr = masterModel.Sum(expr, masterModel.Prod(A_Matrix[j][i], (INumVar)X[j]));
                }
                expr = masterModel.Sum(expr, masterModel.Prod(-1, (INumVar)ExtraCovered[i]), (INumVar)Uncovered[i]);
                //Constraint[i] = masterModel.AddGe(expr, 1);
                Constraint[i] = masterModel.AddEq(expr, 1);
            }


        }

        public void Build_RMP_General(InitialSolution IS)
        {            
            //NodeSet = IS.NodeSet;
            initialPath_num = IS.PathSet.Count;                       
            trip_num = TripList.Count;
            realistic_trip_num = NetWork.num_Physical_trip;

            X = new List<INumVar>();
            Coefs = new List<double>();
            A_Matrix = new List<int[]>();
            PathSet = new List<Pairing>();

            //IS.PrepareInputForRMP(TripList);
            Coefs = IS.Coefs;
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
                X.Add(var);
                Obj.Expr = masterModel.Sum(Obj.Expr, masterModel.Prod(Coefs[j], X[j]));
            }         
            //constraints
            for (i = 0; i < realistic_trip_num; i++)
            {
                INumExpr expr = masterModel.NumExpr();
                
                for (j = 0; j < initialPath_num; j++)
                {                   
                    expr = masterModel.Sum(expr, 
                                           masterModel.Prod(A_Matrix[j][i], X[j]));//在从初始解传值给A_Matrix，已经针对网络复制作了处理
                }
                
                Constraint[i] = masterModel.AddGe(expr, 1);
            }
        }        

        public void LinearRelaxation() 
        {            
            
            Node s = NodeSet[0];
            Topological2 Topo = new Topological2(Network, s);
            RCSPP R_C_SPP = new RCSPP(Topo.Order);
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
                    X.Add(newColumn);
                    A_Matrix.Add(newAji);
                    Coefs.Add(newCoef);
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
            Build_RMP_General(IS);

            root_node = new TreeNode();
            CG(root_node);

            var_to_branch = new Stack<INumVar>();
            //TODO:添加函数：将第一次求解，即根节点结果存储，包括变量值，目标函数值等
            


            Branch_and_Bound(root_node);

        
        }

        #region 新增，2018-12-11，看完分支定界后的想法

        public void CG(TreeNode tree_node)
        {
            //IS
            //BuildRMP
            int result = SolveRMP();
            int i;

            RCSPP rcspp = new RCSPP();//至始至终定价问题都只有一个实体，求解过程中只是改变各属性值
            List<Pairing> New_Columns;
            double[] reduced_costs;
            int[,] new_MultiAjis;

            for (; ; )
            {
                //if (IsRelaxOpt(ref rcspp))
                //    break;
                //Console.WriteLine("检验数(min)： " + rcspp.Reduced_Cost);
                //TODO:add columns
                //New_Columns = rcspp.New_Columns;
                //reduced_costs = rcspp.reduced_costs;
                //new_MultiAjis = rcspp.newMultiAji;
                //for ( i = 0; i < New_Columns.Count; i++)
                //{

                //}
            }


        }
        public void Branch_and_Bound(TreeNode root_node) 
        {
            double UB = int.MaxValue;
            double LB = root_node.obj_value;
            
            while (TerminationCondition() == false) 
            {
                if (CheckFeasible(root_node) == false)
                {
                    if (root_node.obj_value > UB) //不必在该点继续分支
                    {
                        /*没有节点（变量）可以回溯，所有变量分支过了
                        * 没有变量可以分支，所有变量都分支过了
                         */
                        if (root_node.fixed_vars.Count == 0
                            || root_node.not_fixed_var_value_pairs.Count == 0)
                        {
                            Console.WriteLine("找不到可分支的变量");
                            break;
                        }

                        Backtrack(ref root_node.fixed_vars);
                        SolveChildNode(ref root_node);
                        //continue;
                    }
                    else //root_node.obj_value <= UB,有希望，继续分支
                    {
                        LB = root_node.obj_value;
                        SolveChildNode(ref root_node);
                        //continue;
                    }
                }
                else //可行，更新上界
                {
                    //TODO:可抽离为函数 2-23-2019
                    if (root_node.obj_value > UB) //不必在该点继续分支
                    {
                        /*没有节点（变量）可以回溯，所有变量分支过了
                        * 没有变量可以分支，所有变量都分支过了
                         */
                        if (root_node.fixed_vars.Count == 0
                            || root_node.not_fixed_var_value_pairs.Count == 0)
                        {
                            Console.WriteLine("找不到可分支的变量");
                            break;
                        }

                        Backtrack(ref root_node.fixed_vars);
                        SolveChildNode(ref root_node);
                        
                    }
                    else //root_node.obj_value <= UB，更新UB，停止在该点分支，回溯
                    {
                        UB = root_node.obj_value;
                        RecordNodeSolution(root_node);

                        if ((UB - LB) / UB < GAP) 
                        {                            
                            break;
                        }

                        Backtrack(ref root_node.fixed_vars);
                        SolveChildNode(ref root_node);                        
                    }
                }
            }
        }
        bool TerminationCondition() 
        {

            return true;
        }
        bool CheckFeasible(TreeNode node) 
        {
            double epsilon = 1e-12;
            foreach (var var_value in node.not_fixed_var_value_pairs) 
            {                                
                if (Math.Abs(var_value.Value - Convert.ToInt32(var_value.Value)) > epsilon) //不是整数，不可行（浮点型不用 == ，!=比较）
                {
                    return false;
                }
            }
            return true;
        }
        void Backtrack(ref List<INumVar> fixed_vars) 
        {                        
            fixed_vars.RemoveAt(fixed_vars.Count - 1);
        }
        void Branch(ref TreeNode node)
        {
            //找字典最大Value对应的key
            INumVar var_maxvalue = node.not_fixed_var_value_pairs.First().Key;
            double maxvalue = node.not_fixed_var_value_pairs.First().Value;

            foreach (var var_value in node.not_fixed_var_value_pairs) 
            {
                var_maxvalue = maxvalue > var_value.Value ? var_maxvalue : var_value.Key;
            }

            node.fixed_vars.Add(var_maxvalue);
            node.not_fixed_var_value_pairs.Remove(var_maxvalue);
        }
        void RecordNodeSolution(TreeNode node)//TODO:记录值为分数的var，即待分支的var 2-23-2019
        {
            int i;
            double value_var;
            Value_Column = new Dictionary<int, double>();
            for (i = 0; i < X.Count; i++)
            {
                value_var = masterModel.GetValue(X[i]);
                if (!(value_var == 0 || value_var == 1))
                    Value_Column[i] = masterModel.GetValue(X[i]);
            }
        }

        public void SolveChildNode(ref TreeNode node) 
        {            
            
            Branch(ref root_node);
            CG(root_node);           
        }
        

        public int SolveRMP()
        {
            int status = 0;
            try
            {
                if (masterModel.Solve())
                {
                    Console.WriteLine("{0}: {1}", "RMP ObjValue", masterModel.GetObjValue());
                    //Console.WriteLine("iter time: ",Linear_iter);
                }
                else
                {
                    throw new ILOG.Concert.Exception();
                }

            }
            catch (ILOG.Concert.Exception ex)
            {
                Console.WriteLine("RMP can't solved, there might exit some error");
                Console.WriteLine("{0} from {1}", ex.Message, ex.Source);
            }
            return status;
        }
        

        bool IsRelaxOpt(ref RCSPP rcspp) //求解子问题，判断 检验数 < 0 ?
        {
            Change_Arc_Length();
            rcspp.ShortestPath("Forward");

            bool stopLP = rcspp.FindMultipleNewPath(num_AddColumn);//T-opt;F-iter

            return stopLP;//未达到最优，继续生成新列
        }


        #region //分支
        void RecordCurrentOpt()
        {
            int i;
            double v;
            Pairing Column;
            for (i = 0; i < ColumnPool.Count; i++)
            {
                Column = ColumnPool[i];
                v = masterModel.GetValue((INumVar)X[i]);
                if (v == 1)
                {
                    OptColumn.Add(Column);
                }
            }
            // double fixedVar = Value_Column.Max(var => var.Value);
        }
        
        #endregion

        #endregion
        
        

    }
        
        

}
