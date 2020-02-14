using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FastData.Core;
using FastData.Core.Context;
using FastEtlWeb.DataModel;
using FastData.Core.Model;

namespace FastEtlWeb.Pages
{
    public class DicTypeFormModel : PageModel
    {
        public Data_Dic info = new Data_Dic();

        public void OnPost(string id)
        {
            using (var db = new DataContext(AppEtl.Db))
            {
                if (!string.IsNullOrEmpty(id))
                    info = FastRead.Query<Data_Dic>(a => a.Id == id).ToItem<Data_Dic>(db);
            }
        }

        /// <summary>
        /// ����
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public IActionResult OnPostDicTypeForm(Data_Dic item)
        {
            var info = new WriteReturn();
            using (var db = new DataContext(AppEtl.Db))
            {
                if (FastRead.Query<Data_Dic>(a => a.Id == item.Id).ToCount(db) == 0)
                {
                    item.Id = Guid.NewGuid().ToString();
                    info = db.Add(item).writeReturn;
                }
                else
                    info = db.Update<Data_Dic>(item, a => a.Id == item.Id).writeReturn;

                if (info.IsSuccess)
                    return new JsonResult(new { success = true, msg = "�����ɹ�" });
                else
                    return new JsonResult(new { success = false, msg = info.Message });
            }
        }

        /// <summary>
        /// ɾ��
        /// </summary>
        /// <returns></returns>
        public IActionResult OnPostDel(string id)
        {
            if (string.IsNullOrEmpty(id))
                return new JsonResult(new { success = false, msg = "ɾ��ʧ��" });
            using (var db = new DataContext(AppEtl.Db))
            {
                if (FastRead.Query<Data_Dic_Details>(a => a.DicId == id).ToCount(db) == 0)
                {
                    if (FastWrite.Delete<Data_Dic>(a => a.Id == id, db).IsSuccess)
                        return new JsonResult(new { success = true, msg = "ɾ���ɹ�" });
                    else
                        return new JsonResult(new { success = false, msg = "ɾ��ʧ��" });
                }
                else
                    return new JsonResult(new { success = false, msg = "ɾ��ʧ�ܴ�����ϸ" });
            }
        }
    }
}
