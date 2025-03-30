using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using YazLab2_Proje1.Models.Entity;


namespace YazLab2_Proje1.Controllers
{
    public class MakaleController : Controller
    {
        //Tablolara ulaşmak için
        AnonimlestirmeSistemiEntities db = new AnonimlestirmeSistemiEntities();
        public ActionResult Index()
        {
            return View();
        }
        [HttpGet]
        public ActionResult Yukleme()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Yukleme(HttpPostedFileBase Makale, string Email)
        {
            if (Makale == null || Makale.ContentLength == 0 || string.IsNullOrEmpty(Email))
            {
                TempData["Message"] = "Lütfen tüm alanları doldurun ve bir dosya seçin!";
                return RedirectToAction("Yukleme");
            }

            var mevcutKullanici = db.Tbl_Users.FirstOrDefault(u => u.Email == Email);

            if (mevcutKullanici == null)
            {
                mevcutKullanici = new Tbl_Users
                {
                    Email = Email,
                    Role = "Yazar" 
                };
                db.Tbl_Users.Add(mevcutKullanici);
                db.SaveChanges();
            }


            string fileName = Path.GetFileName(Makale.FileName);
            string makaleBaslik = Path.GetFileNameWithoutExtension(fileName); 

            string fileExtension = Path.GetExtension(fileName).ToLower();
            if (fileExtension != ".pdf")
            {
                TempData["Message"] = "Sadece PDF dosyaları yüklenebilir.";
                return RedirectToAction("Yukleme");
            }

            string uploadDirectory = Server.MapPath("~/MakaleDosyalari/");
            if (!Directory.Exists(uploadDirectory))
            {
                Directory.CreateDirectory(uploadDirectory);
            }

            string filePath = Path.Combine(uploadDirectory, fileName);
            Makale.SaveAs(filePath);

            Guid trackingNumber;
            do
            {
                trackingNumber = Guid.NewGuid();
            } while (db.Tbl_Articles.Any(a => a.TrackingNumber == trackingNumber));

            var yeniMakale = new Tbl_Articles
            {
                UserID = mevcutKullanici.UserID,
                Title = makaleBaslik, 
                FilePath = "/MakaleDosyalari/" + fileName,
                SubmissionDate = DateTime.Now,
                Status = "Yüklenmiş",
                TrackingNumber = trackingNumber,
                Anonim = false
            };

            db.Tbl_Articles.Add(yeniMakale);
            db.SaveChanges();

            LogEkle(yeniMakale.ArticleID, "Makale yazar tarafından sisteme yüklendi.");
            TempData["TrackingNumber"] = trackingNumber.ToString();
            TempData["Message"] = "Makale başarıyla yüklendi!";
            return RedirectToAction("Yukleme");
        }

        public void LogEkle(int articleId, string mesaj)
        {
            using (var db = new AnonimlestirmeSistemiEntities())
            {
                db.Tbl_Logs.Add(new Tbl_Logs
                {
                    ArticleID = articleId,
                    LogDetails = mesaj,
                    LogDate = DateTime.Now
                });
                db.SaveChanges();
            }
        }

        public ActionResult Sorgulama()
        {
            return View();
        }
        [HttpPost]
        public ActionResult Sorgulama(string TakipNo, string Email)
        {
            if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(TakipNo))
            {
                TempData["Message"] = "Lütfen tüm alanları doldurun!";
                return RedirectToAction("Sorgulama");
            }

            var makale = db.Tbl_Articles.FirstOrDefault(m => m.TrackingNumber.ToString() == TakipNo && m.Tbl_Users.Email == Email);

            if (makale == null)
            {
                TempData["Message"] = "Girdiğiniz bilgilerle eşleşen bir makale bulunamadı.";
                return RedirectToAction("Sorgulama");
            }

            return View("MakaleDetay", makale); 
        }


    }
}