using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

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

            IS.GetFeasibleSolutionByPenalty();
            //IS.GetFeasibleSolutionByMethod1();//顺逆向标号在多基地时有问题：如对点i，顺向时最短路对应基地为B1,逆向时最短路对应基地为B2.错误
            sw.Stop();

            Report Report_IS = new Report(IS.PathSet);
            
            Console.WriteLine(Report_IS.TransferSolution());

            Console.WriteLine("平均纯乘务时间：{0} 平均换乘时间{1} 平均task数{2}", Report_IS.summary_mean.mean_PureCrew
                , Report_IS.summary_mean.mean_Trans, Report_IS.summary_mean.mean_Tasks);
            Report_IS.WriteCrewPaths(@"D:\代码项目\C#\CG-version2.1_cost_1440-重构\CG_CSP_1440\ISS_LineList.txt");

            Console.WriteLine(sw.Elapsed.TotalSeconds);
            //checked：OK
            /*********<<下面开始测试CG>>***************************************************************************/
            CSP csp = new CSP(Net);
            csp.Branch_and_Price(IS);
            int y = 0;
            
        }
    }
    public class Report 
    {
        StringBuilder pathStr;
        List<Pairing> pathset;

        public struct SummarySingleDuty 
        {
            public double totalLength,
            totalConnect,
            pureCrewTime,
            externalRest;

            public void SetValue(double length, double connect, double pure_crewTime, double external_rest) 
            {
                totalLength = length;
                totalConnect = connect;
                pureCrewTime = pure_crewTime;
                externalRest = external_rest;
            }
        }
        public SummarySingleDuty summary_single = new SummarySingleDuty();
        public struct Summary_Mean 
        {
            public double mean_PureCrew,
            mean_Trans,
            mean_Tasks;

            public void SetValue(double pureTime, double transTime, double tasks) 
            {
                mean_PureCrew = pureTime;
                mean_Trans = transTime;
                mean_Tasks = tasks;
            }
        }
        public Summary_Mean summary_mean = new Summary_Mean();
        public struct Summary_Algotithm 
        {
            public double appear_time_FirstFeasibleSolution,
            GAP_FirstFeasibleSolution,
            GAp_Opt,
            total_TreeNodes,
            total_Columns;
        }

        public Report(List<Pairing> PathSet) 
        {
            pathStr = new StringBuilder();
            pathset = PathSet;
        }

        public StringBuilder TransferSolution()
        {            
            int pathindex = 0;
            int sum_duties = 0;

            summary_mean.SetValue(0, 0, 0);

            foreach (Pairing path in pathset)
            {
                ++pathindex;
                summary_single.SetValue(0, 0, 0, 0);
                
                //start_time = 0; end_time = 0; num_external_days = 0;
                pathStr.AppendFormat("乘务交路{0}: ", pathindex);

                translate_single_pairing(path, ref pathStr, ref summary_single);
                //TODO:计算平均值
                sum_duties += Convert.ToInt32(path.Coef / 1440);
                summary_mean.mean_PureCrew += summary_single.pureCrewTime;
                summary_mean.mean_Trans += summary_single.totalConnect - summary_single.externalRest;
                summary_mean.mean_Tasks += path.Arcs.Count - 3;
            }
            cal_MeanSummary(sum_duties, ref summary_mean);

            return pathStr;
        }

        public StringBuilder translate_single_pairing(Pairing path, ref StringBuilder pathStr, ref SummarySingleDuty summary) 
        {
            double start_time = 0, end_time = 0;
            int num_external_days = 0;

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
                        summary.totalConnect += arc.Cost;
                        break;
                    case 22:
                        pathStr.AppendFormat("{0} {1}", arc.O_Point.TrainCode, "→");
                        summary.totalConnect += arc.Cost;
                        summary.externalRest += arc.Cost;
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
            summary.totalLength = end_time - start_time;
            summary.pureCrewTime = summary.totalLength - summary.totalConnect;

            pathStr.AppendFormat(" 总长度 {0}\t纯乘务时间 {1}\t总接续时间 {2}\t外驻时间 {3}",
                summary.totalLength, summary.pureCrewTime, summary.totalConnect, summary.externalRest);
            
            pathStr.AppendLine();
            
            return pathStr;
        }

        void cal_MeanSummary(int sum_duties, ref Summary_Mean s_mean) 
        {
            s_mean.mean_PureCrew /= sum_duties;
            s_mean.mean_Trans /= sum_duties;
            s_mean.mean_Tasks /= sum_duties;
        }

        public void WriteCrewPaths(string file)
        {
            StreamWriter Crew_paths = new StreamWriter(file);

            TransferSolution();
            Crew_paths.WriteLine(this.pathStr);

            Crew_paths.Close();


        }

    }
}
