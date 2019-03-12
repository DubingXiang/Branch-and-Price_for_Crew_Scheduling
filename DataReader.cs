using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using Dubin_Data;

namespace CG_CSP_1440
{
    class DataReader
    {
        //input
        public SqlConnection sqlConn = null;
        public SqlCommand sqlComm = null;
        public SqlDataAdapter sqlDataAdapter = null;
        public SqlCommandBuilder sqlCommBuilder = null;

        public DataSet Ds   = null;
        public DataTable Dt = null;
        //out
        public List<Node> NodeSet;        
        public List<Node> TripList;
        public List<Node> ODBaseList;

        public static List<CrewBase> CrewBaseList;

        public Dictionary<string, CSVReader> CSV_Set_;
        public CSVReader CSV_;

        /// <summary>
        /// connect SQL,Read table,store data in DataSet
        /// </summary>
        /// <param name="connStr"></param>
        /// <returns></returns>
        public DataSet ConnSQL(string connStr)
        {
            //连接数据库，打开
            //取出表中数据，放入DataSet中，然后进行读取
            //创建适配器，用于读取 sqlConn连接的数据库中 表 sql 的数据
            //在Ds中新建一个名为“Timetable”的DataTable，并将读取的数据填入(fill)到其中
            //将Dataset的操作更新至数据库
            //关闭
            sqlConn = new SqlConnection(connStr);
            sqlConn.Open();

            Ds = new DataSet("乘务计划数据集");

            string sql = "SELECT * FROM [Timetable$]";
            //string sql = "SELECT * FROM [程岩岩$]";
            //string sql = "SELECT * FROM [Bei_Tian$]";

            sqlDataAdapter = new SqlDataAdapter(sql, sqlConn);
            sqlDataAdapter.Fill(Ds,"Timetable");
            //sqlDataAdapter.Fill(Ds, "程岩岩");
            //sqlDataAdapter.Fill(Ds, "Bei_Tian");
            sqlCommBuilder = new SqlCommandBuilder(sqlDataAdapter);

            sql = "select * from [CrewBase$]";//[] 区分开表名与SQL保留关键字
            sqlDataAdapter = new SqlDataAdapter(sql, sqlConn);
            sqlDataAdapter.Fill(Ds, "CrewBase");
            sqlCommBuilder = new SqlCommandBuilder(sqlDataAdapter);

            sqlConn.Close();
            return Ds;
        }
        /// <summary>
        /// Load data from connected DataSet,Get (copyed) NodeSet
        /// 加载数据，即可得到点集，对点的各项成员初始化
        /// </summary>
        /// <param name="Ds"></param>
        /// <param name="MaxDays"></param>
        public void LoadData_sql(DataSet Ds, int MaxDays)
        {
            //Dt = Ds.Tables["程岩岩"];
            //Dt = Ds.Tables["Bei_Tian"];
            Dt = Ds.Tables["Timetable"];
            NodeSet = new List<Node>();
            TripList = new List<Node>();
            ODBaseList = new List<Node>();
            DataTable Dt_Base = new DataTable();
            Dt_Base = Ds.Tables["CrewBase"];
            CrewBaseList = new List<CrewBase>();
            int i, j = 0;

            #region //单基地虚拟起终点
            Node virO = new Node();
            virO.ID = 0;
            virO.LineID = 0;
            virO.RoutingID = 0;
            virO.TrainCode = "";
            virO.StartTime = 0;
            virO.StartStation = "";
            virO.EndTime = 0;
            virO.EndStation = virO.StartStation;
            virO.Length = 0;
            virO.Type = 10;
            //Label virOlabel = new Label();
            //virOlabel.VisitedCount = new int[Dt.Rows.Count + 1];            
            //virO.LabelsForward.Add(virOlabel);
            NodeSet.Add(virO);
            Node virD = new Node();
            virD.ID = 1;
            virD.LineID = virO.LineID;
            virD.RoutingID = virO.RoutingID;
            virD.TrainCode = virO.TrainCode;
            virD.StartTime = 1440 * MaxDays;
            virD.StartStation = virO.StartStation;
            virD.EndTime = virD.StartTime;
            virD.EndStation = virD.StartStation;
            virD.Length = 0;
            virD.Type = 20;
            NodeSet.Add(virD);
            #endregion
            int k = 2;
            for (i = 0; i < Dt_Base.Rows.Count; i++) //乘务基地及其起终点
            {
                CrewBase Base;
                Base.ID = i + 1;
                Base.Station = Convert.ToString(Dt_Base.Rows[i]["乘务基地"]);
                CrewBaseList.Add(Base);
                
                Node OBase = new Node();
                OBase.ID = k++;
                OBase.LineID = 0;
                OBase.RoutingID = 0;
                OBase.TrainCode = "";
                OBase.StartTime = 0;
                OBase.StartStation = Convert.ToString(Dt_Base.Rows[i]["乘务基地"]);//乘务基地所在地
                OBase.EndTime = 0;
                OBase.EndStation = OBase.StartStation;
                OBase.Length = 0;
                OBase.Type = 0; //10-virO, 20-virD, 0-OBase, 2-DBase，1-trip

                Node DBase = new Node();
                DBase.ID = k++;
                DBase.LineID = OBase.LineID;
                DBase.RoutingID = OBase.RoutingID;
                DBase.TrainCode = OBase.TrainCode;
                DBase.StartTime = 1440 * MaxDays;
                DBase.EndTime = 1440 * MaxDays;
                DBase.StartStation = OBase.StartStation;
                DBase.EndStation = DBase.StartStation;
                DBase.Type = 2;
                //Label的初始化在REF中完成
                ODBaseList.Add(OBase);
                ODBaseList.Add(DBase);
                NodeSet.Add(OBase);
                NodeSet.Add(DBase);  
            }
            k = 2 + 2 * CrewBaseList.Count - 1;
            for (i = 0; i < Dt.Rows.Count; i++)
            {
                Node trip           = new Node();
                trip.LineID         = Convert.ToInt32(Dt.Rows[i]["编号"]);
                trip.ID             = Convert.ToInt32(Dt.Rows[i]["编号"]) + k;
                trip.TrainCode      = Convert.ToString(Dt.Rows[i]["车次"]);
                trip.RoutingID      = Convert.ToInt32(Dt.Rows[i]["交路编号"]);
                trip.StartStation   = Convert.ToString(Dt.Rows[i]["出发车站"]);
                trip.EndStation     = Convert.ToString(Dt.Rows[i]["到达车站"]);
                trip.StartTime      = Convert.ToInt32(Dt.Rows[i]["出发时刻"]);
                trip.EndTime        = Convert.ToInt32(Dt.Rows[i]["到达时刻"]);
                trip.Length         = trip.EndTime - trip.StartTime;
                trip.Type           = 1;
                TripList.Add(trip);             
                NodeSet.Add(trip); 
            }
            //copy trips_node
            int trip_number = TripList.Count;
            int end = TripList.Last().ID;
            Node temp;
            for (j = 1; j < MaxDays; j++) {//复制 MaxDays - 1次，得到 Maxday 天的网
                for (i = k+1; i <= end; i++) {
                    temp                  = NodeSet[i];
                    Node c_trip           = new Node();
                    c_trip.ID             = temp.ID + trip_number * j;
                    c_trip.LineID         = temp.LineID;
                    c_trip.TrainCode      = temp.TrainCode;
                    c_trip.RoutingID      = temp.RoutingID;
                    c_trip.StartStation   = temp.StartStation;
                    c_trip.EndStation     = temp.EndStation;
                    c_trip.StartTime      = temp.StartTime + 1440 * j;
                    c_trip.EndTime        = temp.EndTime + 1440 * j;
                    c_trip.Length         = temp.Length;
                    c_trip.Type           = 1;
                    TripList.Add(c_trip);
                    NodeSet.Add(c_trip);
                }
            }                                           
        }

        public void Connect_csvs(out List<string> csv_files) 
        {            
            csv_files = new List<string>();
            //以相对路径加载文件，故文件必须放在 bin文件夹里的debug 或 release文件夹里的 data文件夹里
            csv_files.Add(@"\data\Timetable.csv");
            csv_files.Add(@"\data\CrewBase.csv");

            CSVReader CSV = new CSVReader();
            CSV_Set_ = CSV.Read_Multi_CSVs(csv_files);

        }

        public void LoadData_csv(int MaxDays) 
        {
            CSV_ = CSV_Set_["Timetable.csv"];
            Dictionary<string, List<string>> Timetable_Columns = CSV_.Data_Set_;

            CSVReader CSV_Base = CSV_Set_["CrewBase.csv"];
            Dictionary<string, List<string>> Base_Columns = CSV_Base.Data_Set_;
            List<string> Base_col = Base_Columns["乘务基地"];

            NodeSet = new List<Node>();
            TripList = new List<Node>();
            ODBaseList = new List<Node>();
            CrewBaseList = new List<CrewBase>();
            int i, j = 0;

            #region //单基地虚拟起终点
            Node virO = new Node();
            virO.ID = 0;
            virO.LineID = 0;
            virO.RoutingID = 0;
            virO.TrainCode = "";
            virO.StartTime = 0;
            virO.StartStation = "";
            virO.EndTime = 0;
            virO.EndStation = virO.StartStation;
            virO.Length = 0;
            virO.Type = 10;            
            NodeSet.Add(virO);

            Node virD = new Node();
            virD.ID = 1;
            virD.LineID = virO.LineID;
            virD.RoutingID = virO.RoutingID;
            virD.TrainCode = virO.TrainCode;
            virD.StartTime = 1440 * MaxDays;
            virD.StartStation = virO.StartStation;
            virD.EndTime = virD.StartTime;
            virD.EndStation = virD.StartStation;
            virD.Length = 0;
            virD.Type = 20;
            NodeSet.Add(virD);
            #endregion
            int k = 2;

            for (i = 0; i < Base_col.Count; i++) //乘务基地及其起终点
            {
                CrewBase Base;
                Base.ID = i + 1;
                Base.Station = Convert.ToString(Base_col[i]);
                CrewBaseList.Add(Base);

                Node OBase = new Node();
                OBase.ID = k++;
                OBase.LineID = 0;
                OBase.RoutingID = 0;
                OBase.TrainCode = "";
                OBase.StartTime = 0;
                OBase.StartStation = Convert.ToString(Base_col[i]);//乘务基地所在地
                OBase.EndTime = 0;
                OBase.EndStation = OBase.StartStation;
                OBase.Length = 0;
                OBase.Type = 0; //10-virO, 20-virD, 0-OBase, 2-DBase，1-trip

                Node DBase = new Node();
                DBase.ID = k++;
                DBase.LineID = OBase.LineID;
                DBase.RoutingID = OBase.RoutingID;
                DBase.TrainCode = OBase.TrainCode;
                DBase.StartTime = 1440 * MaxDays;
                DBase.EndTime = 1440 * MaxDays;
                DBase.StartStation = OBase.StartStation;
                DBase.EndStation = DBase.StartStation;
                DBase.Type = 2;
                //Label的初始化在REF中完成
                ODBaseList.Add(OBase);
                ODBaseList.Add(DBase);
                NodeSet.Add(OBase);
                NodeSet.Add(DBase);
            }
            k = 2 + 2 * CrewBaseList.Count - 1;
            for (i = 0; i < Timetable_Columns["编号"].Count; i++)
            {
                Node trip = new Node();
                trip.LineID = Convert.ToInt32(Timetable_Columns["编号"][i]);
                trip.ID = Convert.ToInt32(Timetable_Columns["编号"][i]) + k;
                trip.TrainCode = Convert.ToString(Timetable_Columns["车次"][i]);
                trip.RoutingID = Convert.ToInt32(Timetable_Columns["交路编号"][i]);
                trip.StartStation = Convert.ToString(Timetable_Columns["出发车站"][i]);
                trip.EndStation = Convert.ToString(Timetable_Columns["到达车站"][i]);
                trip.StartTime = Convert.ToInt32(Timetable_Columns["出发时刻"][i]);
                trip.EndTime = Convert.ToInt32(Timetable_Columns["到达时刻"][i]);
                trip.Length = trip.EndTime - trip.StartTime;
                trip.Type = 1;
                TripList.Add(trip);
                NodeSet.Add(trip);
            }
            //copy trips_node
            int trip_number = TripList.Count;
            int end = TripList.Last().ID;
            Node temp;
            for (j = 1; j < MaxDays; j++)
            {//复制 MaxDays - 1次，得到 Maxday 天的网
                for (i = k + 1; i <= end; i++)
                {
                    temp = NodeSet[i];
                    Node c_trip = new Node();
                    c_trip.ID = temp.ID + trip_number * j;
                    c_trip.LineID = temp.LineID;
                    c_trip.TrainCode = temp.TrainCode;
                    c_trip.RoutingID = temp.RoutingID;
                    c_trip.StartStation = temp.StartStation;
                    c_trip.EndStation = temp.EndStation;
                    c_trip.StartTime = temp.StartTime + 1440 * j;
                    c_trip.EndTime = temp.EndTime + 1440 * j;
                    c_trip.Length = temp.Length;
                    c_trip.Type = 1;
                    TripList.Add(c_trip);
                    NodeSet.Add(c_trip);
                }
            }                                           
        }
        

    }
}
