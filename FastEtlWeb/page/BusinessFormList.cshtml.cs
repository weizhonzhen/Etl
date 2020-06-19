using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using FastData.Core.Context;
using FastData.Core.Model;
using FastEtlWeb.Cache;
using FastEtlWeb.DataModel;
using FastRedis.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using FastUntility.Core.Base;
using FastData.Core.Repository;
using FastData.Core;

namespace FastEtlWeb.Pages
{
    public class BusinessFormListModel : PageModel
    {
        public Data_Business_List info = new Data_Business_List();
        private IFastRepository IFast;
        public BusinessFormListModel(IFastRepository _IFast)
        {
            IFast = _IFast;
        }

        /// <summary>
        /// ����
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public IActionResult OnPostBusinessFormList(Data_Business_List item)
        {
            var result = new WriteReturn();
            result.IsSuccess = true;

            using (var db = new DataContext(AppEtl.Db))
            {
                if (IFast.Query<Data_Source>(a => a.Id == item.DataId).ToCount(db) == 0)
                    return new JsonResult(new { success = false, msg = "����Դ������" });

                var data = IFast.Query<Data_Source>(a => a.Id == item.DataId).ToItem<Data_Source>(db);               
                var tableKey = string.Format(AppEtl.CacheKey.Table, data.Host);
                if (!RedisInfo.Exists(tableKey, AppEtl.CacheDb))
                    DataSchema.InitTable(data, false);

                var tableList = RedisInfo.Get<List<CacheTable>>(tableKey, AppEtl.CacheDb);
                foreach (var table in tableList)
                {
                    var columnKey = string.Format(AppEtl.CacheKey.Column, data.Host, table.Name);
                    if (!RedisInfo.Exists(columnKey, AppEtl.CacheDb))
                        DataSchema.InitColumn(data, false, table.Name);

                    var tableModel = BaseMap.CopyModel<Data_Business, Data_Business_List>(item);
                    tableModel.Id = Guid.NewGuid().ToStr();
                    tableModel.Name = string.IsNullOrEmpty(table.Comments) ? table.Name : table.Comments;
                    tableModel.TableName = table.Name;

                    if (result.IsSuccess)
                        result = db.Add(tableModel).writeReturn;

                    if (result.IsSuccess)
                        result = DataSchema.CreateTable(db, tableModel);


                    var columnList = RedisInfo.Get<List<CacheColumn>>(columnKey, AppEtl.CacheDb);
                    var keyName = columnList.Find(a => a.IsKey == true)?.Name;
                    var keyList = columnList.FindAll(a => a.IsKey == true);
                    foreach (var column in columnList)
                    {
                        var columnModel = new Data_Business_Details();
                        columnModel.FieldId = Guid.NewGuid().ToStr();
                        columnModel.Id = tableModel.Id;
                        columnModel.DataSourceId = data.Id;
                        columnModel.TableName = table.Name;
                        columnModel.ColumnName = column.Name;
                        columnModel.FieldName = column.Name;
                        columnModel.Key = keyName;

                        if (result.IsSuccess)
                            result = db.Add(columnModel).writeReturn;
                        else
                            BaseLog.SaveLog(string.Format("tableName:{0},error:", table.Name, result.Message), "Error_CreateTable");

                        if (result.IsSuccess)
                        {
                            if ((keyList.Count > 1 && keyList.Exists(a => a.Name == columnModel.FieldName)))
                                result = DataSchema.AddColumn(db, tableModel, columnModel, column, data, false);
                            else
                                result = DataSchema.AddColumn(db, tableModel, columnModel, column, data);
                            if (result.IsSuccess)
                                DataSchema.UpdateColumnComment(db, tableModel, columnModel, column, data);
                        }
                        result.IsSuccess = true;
                    }

                    if (keyList.Count > 1)
                        DataSchema.AddColumnMoreKey(db, tableModel, keyList);
                }

                if (result.IsSuccess)
                    return new JsonResult(new { success = true, msg = "�����ɹ�" });
                else
                    return new JsonResult(new { success = false, msg = result.Message });
            }
        }
    }

    public class Data_Business_List
    {
        [Required(ErrorMessage = "{0}����Ϊ��")]
        [Display(Name = "ҵ�����")]
        public string DataId { get; set; }

        /// <summary>
        /// ����ʱ��(��)
        /// </summary>
        [Required(ErrorMessage = "{0}����Ϊ��")]
        [Display(Name = "����ʱ��")]
        public decimal? UpdateTime { get; set; }

        /// <summary>
        /// ����Ƶ��(��)
        /// </summary>
        [Required(ErrorMessage = "{0}����Ϊ��")]
        [Display(Name = "����Ƶ��")]
        public decimal UpdateDay { get; set; }

        /// <summary>
        /// ��ȡ����(��)
        /// </summary>
        [Required(ErrorMessage = "{0}����Ϊ��")]
        [Display(Name = "��ȡ����")]
        public decimal UpdateCount { get; set; }

        /// <summary>
        /// �ϴθ���ʱ��
        /// </summary>
        public DateTime LastUpdateTime { get; set; }

        /// <summary>
        /// �������ݴ����
        /// </summary>
        [Required(ErrorMessage = "{0}����Ϊ��")]
        [Display(Name = "�������ݴ����")]
        public decimal SaveDataMonth { get; set; }

        /// <summary>
        /// ������������(1=�ظ�ɾ��,0=�ظ�������2=�ظ�����)
        /// </summary>
        [Required(ErrorMessage = "{0}����Ϊ��")]
        [Display(Name = "������������")]
        public string Policy { get; set; }
    }
}
