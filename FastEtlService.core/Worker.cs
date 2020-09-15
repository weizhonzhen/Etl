using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using FastUntility.Core.Base;
using FastData.Core.Context;
using FastData.Core;
using FastEtlService.core.DataModel;
using FastData.Core.Repository;

namespace FastEtlService.core
{
    public class Worker : BackgroundService
    {
        private readonly IFastRepository IFast;
        public Worker(IFastRepository _IFast)
        {
            IFast = _IFast;
        }

        private Object thisLock = new Object();
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    lock (thisLock)
                    {
                        using (var db = new DataContext(AppEtl.Db))
                        {
                            var tableLink = DataSchema.TableLink(db);
                            BaseLog.SaveLog("��ʼ��ȡ", "FastEtlService");

                            var list = IFast.Query<Data_Business>(a => a.Id != null).ToList<Data_Business>(db);

                            foreach (var item in list)
                            {
                                var isExec = item.UpdateTime == DateTime.Now.Hour;
                                if (item.UpdateTime == 99)
                                    isExec = true;

                                if (DataSchema.IsExistsTable(db, item.TableName) && isExec)
                                {
                                    Parallel.Invoke(() =>
                                    {
                                        var leaf = IFast.Query<Data_Business_Details>(a => a.Id == item.Id).ToList<Data_Business_Details>(db)??new List<Data_Business_Details>();

                                    if (leaf.Count > 0 && leaf.Exists(a => a.Key.ToStr() != ""))
                                    {
                                        var isAdd = true;
                                        var dt = DataSchema.GetTable(tableLink, item.TableName);

                                        var columnName = dt.Columns[2].ColumnName.ToLower();

                                        if (leaf.Exists(a => a.FieldName.ToLower() == columnName))
                                        {
                                            DataSchema.ExpireData(db, item);

                                            //��һ��
                                            var link = DataSchema.InitColLink(leaf, db,IFast);
                                            var tempLeaf = leaf.Find(a => a.FieldName.ToLower() == columnName);
                                            var pageInfo = DataSchema.GetTableCount(tempLeaf, item);

                                            for (var i = 1; i <= pageInfo.pageCount; i++)
                                            {
                                                var log = new Data_Log();
                                                log.Id = Guid.NewGuid().ToStr();
                                                log.TableName = string.Format("{0}_page_{1}", item.TableName, i);
                                                log.BeginDateTime = DateTime.Now;

                                                pageInfo.pageId = i;
                                                var tempLink = link.Find(a => a.GetValue("id").ToStr() == tempLeaf.DataSourceId);
                                                var pageData = DataSchema.GetFirstColumnData(tempLink, tempLeaf, item, pageInfo);

                                                //�������table
                                                for (var row = 0; row < pageData.list.Count; row++)
                                                {
                                                    var dtRow = dt.NewRow();
                                                    dtRow["EtlAddTime"] = DateTime.Now;
                                                    dtRow["EtlKey"] = pageData.list[row].GetValue("key");
                                                    dtRow[columnName] = pageData.list[row].GetValue("data");

                                                    //�ֵ����
                                                    if (!string.IsNullOrEmpty(tempLeaf.Dic))
                                                        dtRow[columnName] = IFast.Query<Data_Dic_Details>(a => a.Value.ToLower() == dtRow[columnName].ToStr().ToLower() && a.DicId == tempLeaf.Dic, a => new { a.ContrastValue }).ToDic(db).GetValue("ContrastValue");

                                                    //���ݲ���
                                                    isAdd = DataSchema.DataPolicy(db, item, dtRow["EtlKey"], columnName, dtRow[columnName]);

                                                    for (var col = 2; col < dt.Columns.Count; col++)
                                                    {
                                                        columnName = dt.Columns[col].ColumnName.ToLower();
                                                        if (leaf.Exists(a => a.FieldName.ToLower() == columnName))
                                                        {
                                                            tempLeaf = leaf.Find(a => a.FieldName.ToLower() == columnName);
                                                            tempLink = link.Find(a => a.GetValue("id").ToStr() == tempLeaf.DataSourceId);
                                                            dtRow[columnName] = DataSchema.GetColumnData(tempLink, tempLeaf, dtRow["EtlKey"]);

                                                            //�ֵ����
                                                            if (!string.IsNullOrEmpty(tempLeaf.Dic))
                                                                dtRow[columnName] = IFast.Query<Data_Dic_Details>(a => a.Value.ToLower() == dtRow[columnName].ToStr().ToLower() && a.DicId == tempLeaf.Dic, a => new { a.ContrastValue }).ToDic(db).GetValue("ContrastValue");

                                                            //���ݲ���
                                                            if (item.Policy == "2")
                                                                isAdd = DataSchema.DataPolicy(db, item, dtRow["EtlKey"], columnName, dtRow[columnName]);
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
                                                IFast.Update<Data_Business>(item, a => a.Id == item.Id, a => new { a.LastUpdateTime }, db);
                                        }
                                    }
                                    });
                                }
                            }

                            BaseLog.SaveLog("������ȡ", "FastEtlService");
                            DataSchema.CloseTableLink(tableLink);
                        }
                    }
                    await Task.Delay(1000 * 60 * 30, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                BaseLog.SaveLog(ex.ToString(), "FastEtlServiceError");
            }
        }
        
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            BaseLog.SaveLog("��������", "FastEtlService");
            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            BaseLog.SaveLog("ֹͣ����", "FastEtlService");
            return base.StopAsync(cancellationToken);
        }
    }
}
