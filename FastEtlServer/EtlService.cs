using FastUntility.Base;
using System;
using System.ServiceProcess;
using System.Timers;
using FastData;
using FastData.Context;
using FastEtlModel.DataModel;
using System.Threading.Tasks;
using FastService.Base;
using System.Collections.Generic;

namespace FastService
{
    public partial class EtlService : ServiceBase
    {
        private Object thisLock = new Object();
        public EtlService()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 开始服务
        /// </summary>
        /// <param name="args"></param>
        protected override void OnStart(string[] args)
        {
            BaseLog.SaveLog("启动服务", "FastEtlService");

            //定时器 轮循时间 半小时
            var timer = new Timer(1000 * 60 * 30);
            timer.Elapsed += new ElapsedEventHandler(SynData);
            timer.Enabled = true;
            timer.AutoReset = true;
        }

        /// <summary>
        /// 结束服务
        /// </summary>
        protected override void OnStop()
        {
            BaseLog.SaveLog("停止服务", "FastEtlService");
        }

        /// <summary>
        /// 系统关机
        /// </summary>
        protected override void OnShutdown()
        {
            while (this.CanStop)
            {
                BaseLog.SaveLog("系统关机停止服务", "FastEtlService");
            }
        }

        /// <summary>
        /// 定时任务同步产品数据
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        public void SynData(object source, ElapsedEventArgs e)
        {
            try
            {
                //防止重复执行
                lock (thisLock)
                {
                    using (var db = new DataContext())
                    {
                        BaseLog.SaveLog("开始抽取", "FastEtlService");

                        //不允许停止服务
                        this.CanStop = false;

                        var list = FastRead.Query<Data_Business>(a => a.Id != null).ToList<Data_Business>(db);

                        foreach (var item in list)
                        {
                            if (DataSchema.IsExistsTable(db, item.TableName) && item.UpdateTime == DateTime.Now.Hour && item.LastUpdateTime.Day + item.UpdateDay >= DateTime.Now.Day)
                            {
                                Parallel.Invoke(() =>
                                 {
                                     var leaf = FastRead.Query<Data_Business_Details>(a => a.Id == item.Id).ToList<Data_Business_Details>(db);

                                     if (leaf.Count > 0)
                                     {
                                         var isAdd = true;
                                         var dt = DataSchema.GetTable(db, item.TableName);
                                         var columnName = dt.Columns[3].ColumnName.ToLower();    
                                         
                                         if (leaf.Exists(a => a.FieldName.ToLower() == columnName))
                                         {
                                             DataSchema.ExpireData(db, item);
                                             
                                             //第一列
                                             var link = DataSchema.InitColLink(leaf, db);
                                             var tempLeaf = leaf.Find(a => a.FieldName.ToLower() == columnName);
                                             var pageInfo = DataSchema.GetTableCount(link[0], tempLeaf, item);

                                             for (var i = 1; i <= pageInfo.pageCount; i++)
                                             {
                                                 var log = new Data_Log();
                                                 log.Id = Guid.NewGuid().ToStr();
                                                 log.TableName = string.Format("{0}_page_{1}", item.TableName, i);
                                                 log.BeginDateTime = DateTime.Now;

                                                 pageInfo.pageId = i;
                                                 var pageData = DataSchema.GetFirstColumnData(link[0], tempLeaf, item, pageInfo);

                                                 //遍历填充table
                                                 for (var row = 0; row < pageData.list.Count; row++)
                                                 {
                                                     var dtRow = dt.NewRow();
                                                     dtRow["Id"] = Guid.NewGuid().ToString();
                                                     dtRow["AddTime"] = DateTime.Now;
                                                     dtRow["Key"] = pageData.list[row].GetValue("key");
                                                     dtRow[columnName] = pageData.list[row].GetValue("data");

                                                     //字典对照
                                                     if (!string.IsNullOrEmpty(tempLeaf.Dic))
                                                         dtRow[columnName] = FastRead.Query<Data_Dic_Details>(a => a.Value.ToLower() == dtRow[columnName].ToStr().ToLower() && a.DicId == tempLeaf.Dic, a => new { a.ContrastValue }).ToDic(db).GetValue("ContrastValue");

                                                     //数据策略
                                                     isAdd = DataSchema.DataPolicy(db, item, dtRow["Key"], columnName, dtRow[columnName]);

                                                     for (var col = 3; col < dt.Columns.Count; col++)
                                                     {
                                                         columnName = dt.Columns[col].ColumnName.ToLower();
                                                         if (leaf.Exists(a => a.FieldName.ToLower() == columnName))
                                                         {
                                                             tempLeaf = leaf.Find(a => a.FieldName.ToLower() == columnName);
                                                             dtRow[columnName] = DataSchema.GetColumnData(link[col-3], tempLeaf, dtRow["Key"]);

                                                             //字典对照
                                                             if (!string.IsNullOrEmpty(tempLeaf.Dic))
                                                                 dtRow[columnName] = FastRead.Query<Data_Dic_Details>(a => a.Value.ToLower() == dtRow[columnName].ToStr().ToLower() && a.DicId == tempLeaf.Dic, a => new { a.ContrastValue }).ToDic(db).GetValue("ContrastValue");

                                                             //数据策略
                                                             if (item.Policy == "2")
                                                                 isAdd = DataSchema.DataPolicy(db, item, dtRow["Key"], columnName, dtRow[columnName]);
                                                         }
                                                     }

                                                     if (isAdd)
                                                         dt.Rows.Add(dtRow);
                                                 }

                                                 if (dt.Rows.Count > 0)
                                                     DataSchema.AddList(db, dt, ref log);
                                                 db.Add(log);
                                                 dt.Clear();
                                             }

                                             DataSchema.CloseLink(link);
                                             item.LastUpdateTime = DateTime.Now;
                                             FastWrite.Update<Data_Business>(item, a => a.Id == item.Id, a => new { a.LastUpdateTime }, db);
                                         }
                                     }
                                 });
                            }
                        }

                        //允许停止服务
                        this.CanStop = true;

                        BaseLog.SaveLog("结束抽取", "FastEtlService");
                    }
                }
            }
            catch (Exception ex)
            {
                BaseLog.SaveLog(ex.ToString(), "FastEtlServiceError");
            }
        }
    }
}
