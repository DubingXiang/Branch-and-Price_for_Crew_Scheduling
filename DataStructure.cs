using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ILOG.Concert;
using ILOG.CPLEX;

namespace CG_CSP_1440
{
    //class DataStructure
    //{

    //}
    public  class Label 
    {
        public int ID;
        public double AccumuCost = 0;//总接续时间，非乘务时间，目标函数系数
        public double AccumuConsecDrive = 0;//从源点至当前点 累计连续驾驶时间，
        public double AccumuDrive = 0;//从源点至当前点 累计驾驶时间，即纯乘务时间
        public double AccumuWork = 0;//即总乘务时间

        public Arc PreEdge;
        public Label PreLabel;
        public bool Dominated = false;
        //TODO：先记着这个成员是刚添加的,net还未建立，故DataFromSQL中virO,VirD的label.VisitedCount.Length = 0,改进
        //对O，D的VisitedCount重新初始化，in DataFromSQL
        public int[] VisitedCount = new int[NetWork.num_Physical_trip];
        public CrewBase BaseOfCurrentPath;//= new CrewBase();//2019-1-25 化多基地为单基地需要对交路加以限制
        //不需要每个Label都new一个，只需new base数量个，然后每个Label的对应optbase指向它

        public Label() 
        {            
            for (int i = 0; i < NetWork.num_Physical_trip; i++)
            {
                VisitedCount[i] = 0;
            }
        }
    }
    public class Pairing 
    {
        public List<Arc> Arcs;
        public double Cost;
        public double Coef;
        public int[] CoverMatrix;//2019-1-26
    }
    public class Node
    {
        private int id;
        public int ID
        {
            get { return id; }
            set { id = value; }
        }
        private int lineID;
        public int LineID
        {
            get { return lineID; }
            set { lineID = value; }
        }
        private string trainCode;
        public string TrainCode
        {
            get { return trainCode; }
            set { trainCode = value; }
        }
        private int routingID;
        public int RoutingID
        {
            get { return routingID; }
            set { routingID = value; }
        }
        private int startTime;
        public int StartTime
        {
            get { return startTime; }
            set { startTime = value; }
        }
        private string startStation;
        public string StartStation
        {
            get { return startStation; }
            set { startStation = value; }
        }
        private int endTime;
        public int EndTime
        {
            get { return endTime; }
            set { endTime = value; }
        }
        private string endStation;
        public string EndStation
        {
            get { return endStation; }
            set { endStation = value; }
        }
        private int type;
        public int Type  //10-virO, 20-virD, 1-trip, 0-OBase, 2-DBase
        {
            get { return type; }
            set { type = value; }
        }
        public double Length;
        public double Price;
        //public bool Visited;
        public List<Label> LabelsForward = new List<Label>();
        public List<Label> LabelsBackward = new List<Label>();
        public List<Arc> Out_Edges = new List<Arc>();
        public List<Arc> In_Edges = new List<Arc>();

        public int numVisited = 0;
    }
    public class Arc
    {
        //private int id;
        //public int ID
        //{
        //    get { return id; }
        //    set { id = value; }
        //}       
        private Node startPoint;
        public Node O_Point
        {
            get { return startPoint; }
            set { startPoint = value; }
        }
        private Node endPoint;
        public Node D_Point
        {
            get { return endPoint; }
            set { endPoint = value; }
        }
        private double cost;//弧长
        public double Cost
        {
            get { return cost; }
            set { cost = value; }
        }
        private int arcType;//弧的类型：1-接续弧，2-出乘弧，3-退乘弧, 20-虚拟起点弧，30-虚拟终点弧
        public int ArcType
        {
            get { return arcType; }
            set { arcType = value; }
        }

    }

    public struct CrewBase {
        public  int ID;
        public string Station;
        
    }

    public class TreeNode 
    {
        public double obj_value;
        public List<int> fixing_vars;
        public Dictionary<int, double> not_fixed_var_value_pairs; //由于每次找最大的Value对应的key，所以可以用一个最大堆来优化
        /// <summary>
        /// 这是回溯"剪枝"剪掉的变量，不得再被二次分支
        /// </summary>
        public List<int> fixed_vars;

        public TreeNode() 
        {
            fixing_vars = new List<int>();
            not_fixed_var_value_pairs = new Dictionary<int, double>();
            fixed_vars = new List<int>();
        }
    }

    //public class Dvar : INumVar 
    //{
    //    public double value_;
    //    public NumVarType type_;

    //    public Dvar() 
    //    {
        
    //    }

    //    public double Value() 
    //    {
        
    //    }

    //}

}
