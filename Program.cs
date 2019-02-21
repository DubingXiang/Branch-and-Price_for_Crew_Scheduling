using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ILOG.Concert;
using ILOG.CPLEX;

namespace CG_CSP_1440
{
    class Program
    {
        static void Main(string[] args)
        {                      
            string data_path = "Server = PC-201606172102\\SQLExpress;DataBase = 乘务计划;Integrated Security = true";
            /*--LOAD DATA FROM SQL--*/
            NetWork Network = new NetWork();            
            Network.CreateNetwork(data_path);
            Network.IsAllTripsCovered();
            //建网，一天的没问题，但未删除出入度为0的点与弧
            //出度为0，说明到达站不在乘务基地，说明其到达时间太晚，无法接续 其他到站为乘务基地的车次
            //入度为0，说明出发站不在乘务基地，说明其出发时间太早，发站为乘务基地的车次 无法接续 到它
            //两天的网，再说
            System.Diagnostics.Stopwatch Net_start = new System.Diagnostics.Stopwatch();            
            Net_start.Start();
            InitialSolution IS = new InitialSolution(Network);//京津，200车次，初始解耗时：107s;建网仅0.6s
            IS.GetFeasibleSolutionByPenalty(); //Add 2-21-2019
            Net_start.Stop();
            TimeSpan initiail_solution = Net_start.Elapsed;
            Console.WriteLine("time for get inition solution : " + initiail_solution.TotalSeconds);

            //test path content of initial feasible solution 
            List<Pairing> initial_Paths = new List<Pairing>();
            initial_Paths = IS.PathSet;
            Node trip = new Node();
            int count = 0;
            int i;
            foreach (Pairing path in initial_Paths)
            {
                Console.WriteLine("route " + count);
                for (i = 0; i < path.Arcs.Count; i++) {
                    trip = path.Arcs[i].D_Point; 
                    Console.Write(trip.ID + " -> ");                    
                }
                count++;       
            }


            /*--BRANCH AND PRICE--*/
            CSP Csp = new CSP(Network);
            //Csp.Build_RMP(IS);
            Csp.Build_RMP_General(IS);
            Csp.LinearRelaxation();

            string LP_result_schedule = "C:\\Users\\Administrator\\Desktop\\LP_CYY2.txt";
            Csp.WriteCrewPaths(LP_result_schedule);

            Cplex masterModel = Csp.masterModel;
            List<INumVar> vars = Csp.X;
            //masterModel.WriteSolutions("D:\\Crew_Solution.txt");
            masterModel.ExportModel("D:\\MP2.lp");

            int  j;
            count = 0;
            for (i = 0; i < vars.Count; i++) 
            {
                if (masterModel.GetValue((INumVar)(vars[i])) >= 0.5) {
                    
                    for (j = 0; j < Csp.PathSet[i].Arcs.Count; j++) {
                        Console.Write(Csp.PathSet[i].Arcs[j].D_Point.ID + " -> ");
                    }
                    //Console.WriteLine();
                    Console.WriteLine("route " + count++);
                }
            }

            for (int t = 0; t < vars.Count; t++) {
                masterModel.Add(masterModel.Conversion((INumVar)vars[t], NumVarType.Int));
            }
            //masterModel.SetParam(Cplex.IntParam.VarSel, 3);//强分支
            //masterModel.SetParam(Cplex.DoubleParam.EpGap, 0.05);//相对GAP
            masterModel.SetParam(Cplex.DoubleParam.TiLim, 1800);//求解时间1200s
            masterModel.Solve();
            masterModel.WriteSolutions("D:\\IP_Solutions");

            string IP_result_schedule = "C:\\Users\\Administrator\\Desktop\\IP_Crew_Schedule2.txt";
            Csp.WriteCrewPaths(IP_result_schedule);

            count = 0;
            for (i = 0; i < vars.Count; i++)
            {
                if (masterModel.GetValue((INumVar)(vars[i])) >= 0.5)
                {

                    for (j = 0; j < Csp.PathSet[i].Arcs.Count; j++)
                    {
                        Console.Write(Csp.PathSet[i].Arcs[j].D_Point.ID + " -> ");
                    }
                    //Console.WriteLine();
                    Console.WriteLine("route " + count++);
                }
            }
            int all_covered_num = 0; count = 0;
            int[] trip_coverd = new int[151];
            for (i = 0; i < 151; i++) {
                trip_coverd[i] = 0;
            }
            for (i = 0; i < vars.Count; i++)
            {
                if (masterModel.GetValue((INumVar)(vars[i])) >= 0.5)
                {

                    Console.Write("route " + (++count) + ",");
                    Console.Write(Csp.PathSet[i].Arcs.Count - 1 + "\t");
                    all_covered_num = all_covered_num + Csp.PathSet[i].Arcs.Count - 1;
                    Node trip2 = new Node();
                    for (j = 0; j < Csp.PathSet[i].Arcs.Count; j++)
                    {
                        if (Csp.PathSet[i].Arcs[j].O_Point.LineID != 0)
                        {
                            for (int k = 0; k < 151; k++)
                            {
                                if (Csp.PathSet[i].Arcs[j].O_Point.LineID == (k+1))
                                {
                                    trip_coverd[k]++;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            Console.WriteLine("all covered node num:" + all_covered_num);
            for (int t = 0; t < trip_coverd.Length;t++ )
            {
                if (trip_coverd[t] < 1) {
                    Console.Write("<< trip" + (t + 1) + " coverd " + trip_coverd[t] + " times " + " >>  ");
                }
            }
            
        }
    }
}
