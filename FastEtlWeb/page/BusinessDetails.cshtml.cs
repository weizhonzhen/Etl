using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FastUntility.Core.Base;
using FastData.Core;
using FastData.Core.Context;
using FastRedis.Core;
using FastEtlWeb.DataModel;
using System.Data.Common;
using FastEtlWeb.Cache;
using System.ComponentModel.DataAnnotations;
using FastData.Core.Model;
using FastData.Core.Repository;

namespace FastEtlWeb.Pages
{
    public class BusinessDetailsModel : PageModel
    {
        private readonly IFastRepository IFast;
        public BusinessDetailsModel(IFastRepository _IFast)
        {
            IFast = _IFast;
        }

        public List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
        public string BusinessId = string.Empty;
        public void OnGet(string id)
        {
            using (var db = new DataContext(AppEtl.Db))
            {
                var param = new List<DbParameter>();
                var item = DbProviderFactories.GetFactory(db.config.DbType).CreateParameter();
                item.ParameterName = "id";
                item.Value = id;
                param.Add(item);
                list = IFast.Query("Business.Details", param.ToArray(), db);
                BusinessId = id;
            }
        }

        public IActionResult OnPostDropList(DropListModel item)
        {
            using (var db = new DataContext(AppEtl.Db))
            {
                if (item.Type.ToLower() == "source")
                {
                    var param = new List<DbParameter>();
                    var temp = DbProviderFactories.GetFactory(db.config.DbType).CreateParameter();
                    temp.ParameterName = "Key";
                    temp.Value = item.Key.ToUpper();
                    param.Add(temp);
                    var data = IFast.Query("DropList.Source", param.ToArray(), db);
                    if (data.Count > 0)
                        return new JsonResult(new { success = true, data = data });
                    else
                        return new JsonResult(new { success = true, data = IFast.Query<Data_Source>(a => a.Id != "").Take(10).ToDics(db) });
                }

                if (item.Type.ToLower() == "table")
                {
                    var host = IFast.Query<Data_Source>(a => a.Id == item.HostId, a => new { a.Host }).ToDic(db).GetValue("host").ToStr();
                    var key = string.Format(AppEtl.CacheKey.Table, host);
                    var data = RedisInfo.Get<List<CacheTable>>(key, AppEtl.CacheDb);
                    data = data.FindAll(a => a.Name.ToUpper().Contains(item.Key.ToUpper()));
                    if (data.Count > 0)
                        return new JsonResult(new { success = true, data = data });
                    else
                        return new JsonResult(new { success = true, data = RedisInfo.Get<List<CacheTable>>(key, AppEtl.CacheDb).Take(10) });
                }

                if (item.Type.ToLower() == "field")
                {
                    var host = IFast.Query<Data_Source>(a => a.Id == item.HostId, a => new { a.Host }).ToDic(db).GetValue("host").ToStr();
                    var key = string.Format(AppEtl.CacheKey.Column, host, item.Table);
                    var data = RedisInfo.Get<List<CacheColumn>>(key, AppEtl.CacheDb);
                    data = data.FindAll(a => a.Name.ToUpper().Contains(item.Key.ToUpper()));
                    if (data.Count > 0)
                        return new JsonResult(new { success = true, data = data });
                    else
                        return new JsonResult(new { success = true, data = RedisInfo.Get<List<CacheColumn>>(key, AppEtl.CacheDb).Take(10) });
                }

                if (item.Type.ToLower() == "dic")
                {
                    var param = new List<DbParameter>();
                    var temp = DbProviderFactories.GetFactory(db.config.DbType).CreateParameter();
                    temp.ParameterName = "Key";
                    temp.Value = item.Key.ToUpper();
                    param.Add(temp);
                    var data = IFast.Query("DropList.Dic", param.ToArray(), db);
                    if (data.Count > 0)
                        return new JsonResult(new { success = true, data = data });
                    else
                        return new JsonResult(new { success = true, data = IFast.Query<Data_Dic>(a => a.Id != "").Take(10).ToDics(db) });
                }

                return new JsonResult(new { success = false });
            }
        }

        /// <summary>
        /// ����
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public IActionResult OnPostBusinessDetailsForm(Data_Business_Details item)
        {
            var info = new WriteReturn();
            using (var db = new DataContext(AppEtl.Db))
            {
                var table = IFast.Query<Data_Business>(a => a.Id == item.Id).ToItem<Data_Business>(db);
                var source = IFast.Query<Data_Source>(a => a.Id == item.DataSourceId).ToItem<Data_Source>(db);
                var key = string.Format(AppEtl.CacheKey.Column, source.Host, item.TableName);
                var colunm = RedisInfo.Get<List<CacheColumn>>(key, AppEtl.CacheDb).Find(a => a.Name == item.ColumnName);

                db.BeginTrans();
                if (IFast.Query<Data_Business_Details>(a => a.FieldId == item.FieldId).ToCount(db) == 0)
                {
                    item.FieldId = Guid.NewGuid().ToStr();
                    info = IFast.Add(item);
                    if (info.IsSuccess)
                    {
                        info= DataSchema.AddColumn(db, table, item, colunm, source);
                        if (info.IsSuccess)
                            DataSchema.UpdateColumnComment(db, table, item, colunm, source);
                    }
                }
                else
                {
                    info = IFast.Update<Data_Business_Details>(item, a => a.FieldId == item.FieldId);
                    if (info.IsSuccess)
                    {
                        info.IsSuccess = DataSchema.UpdateColumn(db, table, item, colunm, source);
                        if (info.IsSuccess)
                            DataSchema.UpdateColumnComment(db, table, item, colunm, source);
                    }
                }

                if (info.IsSuccess)
                {
                    db.SubmitTrans();
                    return new JsonResult(new { success = true, msg = "�����ɹ�" });
                }
                else
                {
                    db.RollbackTrans();
                    return new JsonResult(new { success = false, msg = "����ʧ��" });
                }
            }
        }

        /// <summary>
        /// ɾ����
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public IActionResult OnPostDel(string id)
        {
            using (var db = new DataContext(AppEtl.Db))
            {
                if (IFast.Query<Data_Business_Details>(a => a.FieldId == id).ToCount(db) == 0)
                    return new JsonResult(new { success = false, msg = "����ʧ��" });
                else
                {
                    var colunm = IFast.Query<Data_Business_Details>(a => a.FieldId == id).ToItem<Data_Business_Details>(db);
                    var table = IFast.Query<Data_Business>(a => a.Id == colunm.Id).ToItem<Data_Business>(db);

                    if (DataSchema.DropColumn(db, table, colunm) && IFast.Delete<Data_Business_Details>(a => a.FieldId == id).IsSuccess)
                        return new JsonResult(new { success = true, msg = "�����ɹ�" });
                    else
                        return new JsonResult(new { success = false, msg = "����ʧ��" });
                }
            }
        }
    }

    public class DropListModel
    {
        [Required(ErrorMessage = "{0}����Ϊ��")]
        [Display(Name ="����")]
        public string Type { get; set; }

        [Required(ErrorMessage = "{0}����Ϊ��")]
        [Display(Name = "�ؼ���")]
        public string Key { get; set; }

        [CheckDropList]
        [Display(Name = "����")]
        public string Table { get; set; }

        [Display(Name = "������")]
        public string HostId { get; set; }
    }

    public class CheckDropListAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var model = validationContext.ObjectInstance as DropListModel;

            if (model.Type.ToLower() == "source")
                return ValidationResult.Success;

            if (model.Type.ToLower() == "table" && string.IsNullOrEmpty(model.HostId))
                return new ValidationResult(ErrorMessage = "����������Ϊ��");
            else if (model.Type.ToLower() == "table")
                return ValidationResult.Success;

            if (model.Type.ToLower() == "field" && string.IsNullOrEmpty(model.HostId))
                return new ValidationResult(ErrorMessage = "����������Ϊ��");
            else if (model.Type.ToLower() == "field" && string.IsNullOrEmpty(model.Table))
                return new ValidationResult(ErrorMessage = "��������Ϊ��");
            else if(model.Type.ToLower() == "field")
                return ValidationResult.Success;

            if (model.Type.ToLower() == "dic")
                return ValidationResult.Success;

            return new ValidationResult(ErrorMessage = "���Ͳ���ȷ");
        }
    }
}
