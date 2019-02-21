using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace CG_CSP_1440
{
    class tempProgram
    {
        static void Main(string[] args) 
        {
            string data_path = "Server = PC-201606172102\\SQLExpress;DataBase = 乘务计划;Integrated Security = true";
            //string data_path = "Server = PC-201606172102\\SQLExpress;DataBase = 乘务计划CSP1440;Integrated Security = true";
            NetWork Net = new NetWork();
            Net.CreateNetwork(data_path); 
            Net.IsAllTripsCovered();
            //检查无误
            InitialSolution IS = new InitialSolution(Net);

            Stopwatch sw = new Stopwatch();
            sw.Start();

            //IS.GetFeasibleSolutionByPenalty();
            IS.GetFeasibleSolutionByMethod1();//顺逆向标号在多基地时有问题：如对点i，顺向时最短路对应基地为B1,逆向时最短路对应基地为B2.错误
            sw.Stop();

            Report Report_IS= new Report();
            Report_IS.PrintSolution(IS.PathSet);

            Console.WriteLine(sw.Elapsed.TotalSeconds);
            //checked：OK

            
        }
    }
    public class Report 
    {
        public void PrintSolution(List<Pairing> PathSet)
        {
            StringBuilder pathStr = new StringBuilder();
            int pathindex = 0;
            double totalLength, totalConnect, externalRest;
            double start_time, end_time;
            int num_external_days;

            foreach (Pairing path in PathSet)
            {
                ++pathindex;
                totalLength = 0; totalConnect = 0; externalRest = 0; start_time = 0; end_time = 0; num_external_days = 0;
                pathStr.AppendFormat("乘务交路{0}: ", pathindex);
                foreach (Arc arc in path.Arcs)
                {                    
                    switch (arc.ArcType) 
                    {
                        case 2:
                            pathStr.AppendFormat("{0}站{1}分出乘", arc.O_Point.StartStation, arc.D_Point.StartTime);
                            start_time = arc.D_Point.StartTime;
                            break;
                        case 1:
                            pathStr.AppendFormat("{0} {1}", arc.O_Point.TrainCode, "→");
                            totalConnect += arc.Cost;
                            break;
                        case 22:
                            pathStr.AppendFormat("{0} {1}", arc.O_Point.TrainCode, "→");
                            totalConnect += arc.Cost;
                            externalRest += arc.Cost;
                            num_external_days++;
                            break;
                        case 3:
                            pathStr.AppendFormat("{0} {1}站{2}分退乘", arc.O_Point.TrainCode, arc.D_Point.EndStation, arc.O_Point.EndTime);
                            end_time = arc.O_Point.EndTime;
                            break;
                        default:
                            break;
                    }
                }

                totalLength = externalRest > 0 ? end_time - start_time + num_external_days * 1440 : end_time - start_time;

                pathStr.AppendFormat(" 总长度 {0}\t纯乘务时间 {1}\t总接续时间 {2}\t外驻时间 {3}\n", totalLength, totalLength - totalConnect, totalConnect, externalRest);
            }
            Console.WriteLine(pathStr);
        }
    }
}
