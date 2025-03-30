using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using YazLab2_Proje1.Models.Entity;


namespace YazLab2_Proje1.Controllers
{
    public class MessageController : Controller
    {
        AnonimlestirmeSistemiEntities db = new AnonimlestirmeSistemiEntities();
        public ActionResult Messages(int articleId)
        {
            var article = db.Tbl_Articles.FirstOrDefault(a => a.ArticleID == articleId);
            if (article == null)
            {
                return RedirectToAction("Index", "Home"); 
            }

            var owner = db.Tbl_Users.FirstOrDefault(u => u.UserID == article.UserID);
            if (owner == null)
            {
                return RedirectToAction("Index", "Home"); 
            }

            var admin = db.Tbl_Users.FirstOrDefault(u => u.Role == "Editör");
            if (admin == null)
            {
                return RedirectToAction("Index", "Home"); 
            }

            int ownerId = owner.UserID;  
            int adminId = admin.UserID; 

            var messages = db.Tbl_Messages
                .Where(m => m.ArticleID == articleId)
                .OrderBy(m => m.SentDate)
                .ToList();

            ViewBag.ArticleID = articleId;
            ViewBag.UserId = ownerId; 
            ViewBag.AdminId = adminId; 

            return View(messages);
        }

        [HttpPost]
        public ActionResult UserSendMessage(Tbl_Messages newMessage)
        {
            if (newMessage != null && !string.IsNullOrWhiteSpace(newMessage.MessageContent))
            {
                newMessage.SentDate = DateTime.Now;
                db.Tbl_Messages.Add(newMessage);
                db.SaveChanges();
            }

            return RedirectToAction("Messages", new { articleId = newMessage.ArticleID });
        }

        public ActionResult YoneticiMessages(int articleId)
        {
            var article = db.Tbl_Articles.FirstOrDefault(a => a.ArticleID == articleId);
            if (article == null)
            {
                return RedirectToAction("Index", "Yonetici");
            }

            var owner = db.Tbl_Users.FirstOrDefault(u => u.UserID == article.UserID);
            if (owner == null)
            {
                return RedirectToAction("Index", "Yonetici"); 
            }

            var admin = db.Tbl_Users.FirstOrDefault(u => u.Role == "Editör");
            if (admin == null)
            {
                return RedirectToAction("Index", "Yonetici"); 
            }

            int ownerId = owner.UserID;  // Makale sahibi
            int adminId = admin.UserID;  // Yönetici (Admin)

            var messages = db.Tbl_Messages
                .Where(m => m.ArticleID == articleId)
                .OrderBy(m => m.SentDate)
                .ToList();

            ViewBag.ArticleID = articleId;
            ViewBag.UserId = ownerId;
            ViewBag.AdminId = adminId; 
            ViewBag.OwnerEmail = owner.Email;


            return View(messages);
        }

        [HttpPost]
        public ActionResult YoneticiSendMessage(Tbl_Messages newMessage)
        {
            if (!string.IsNullOrWhiteSpace(newMessage.MessageContent))
            {
                newMessage.SentDate = DateTime.Now;
                db.Tbl_Messages.Add(newMessage);
                db.SaveChanges();
            }

            return RedirectToAction("YoneticiMessages", new { articleId = newMessage.ArticleID });
        }



    }
}