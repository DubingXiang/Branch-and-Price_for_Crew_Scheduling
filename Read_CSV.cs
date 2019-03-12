/* ==============================================================================
 * 功能描述：Read_CSV
 * 创 建 者：Dubin
 * 创建日期：2019/3/1 星期五 下午 16:25:53
 * ==============================================================================*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Dubin_Data
{
    class CSVReader
    {
        public string file_path_;
        
        private StreamReader reader_;

        public Dictionary<string, List<string>> Data_Set_;

        public Dictionary<string, CSVReader> CSV_Set;

        public CSVReader() { }

        /// <summary>
        /// filePath只能是文件名，必须放在bin文件夹内
        /// </summary>
        /// <param name="filePath"></param>
        public CSVReader(string filePath) 
        {
            file_path_ = System.Environment.CurrentDirectory;
            //TODO：filePath可以自行选择

            //化相对路径为绝对路径,
            //若输入参数不包括":"，则说明不是绝对路径，故为相对路径，拼接二者。
            //否则，为绝对路径，直接用输入参数
            file_path_ = filePath.Contains(":") == false ? 
                file_path_ + filePath : 
                filePath;
           
            reader_ = new StreamReader(file_path_,Encoding.Default);
            Data_Set_ = new Dictionary<string, List<string>>();
        }

        public Dictionary<string, List<string>> Read(char[] delimete) 
        {
            
            string[] header = reader_.ReadLine().Split(delimete);
            //添加所有列
            List<List<string>> value_list = new List<List<string>>();
            foreach(var s in header) 
            {
                List<string> column = new List<string>();
                value_list.Add(column);                
            }

            string row = reader_.ReadLine();
            string[] str;
            int i;
            while (row != null) 
            {
                str = row.Split(delimete);
                for (i = 0; i < str.Length; i++) 
                {
                    value_list[i].Add(str[i]);
                }

                row = reader_.ReadLine();
            }

            reader_.Close();

            for (i = 0; i < header.Length; i++) 
            {
                Data_Set_.Add(header[i], value_list[i]);
            }

            return Data_Set_;
        }

        /// <summary>
        /// csv_files：所有的表的路径集合,形式为文件名，即："filename.csv"
        /// </summary>
        /// <param name="csv_files"></param>
        public Dictionary<string, CSVReader> Read_Multi_CSVs(List<string> csv_files)
        {
            CSV_Set = new Dictionary<string, CSVReader>();
            string file_name; 
          
            foreach (var file in csv_files)
            {
                CSVReader csv = new CSVReader(file);
                csv.Read(new char[1] { ','});
                file_name = file.Split(new char[1] { '\\' }).Last();
                CSV_Set.Add(file_name, csv);
            }
            return CSV_Set;
        }

    }
}
