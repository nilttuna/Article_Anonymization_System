using iTextSharp.text.pdf.parser;
using iTextSharp.text.pdf;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Web;
using System.Web.Mvc;
using YazLab2_Proje1.Models.Entity;
using Path = System.IO.Path;


namespace YazLab2_Proje1.Controllers
{
    public class YoneticiController : Controller
    {
        // GET: Yonetici

        AnonimlestirmeSistemiEntities db = new AnonimlestirmeSistemiEntities();

        public ActionResult Index()
        {
            var yuklenenMakaleler = db.Tbl_Articles.ToList();

            var anonimMakaleler = db.Tbl_Anonim.Where(m=>m.AnonimStatus=="Anonim").ToList();

            var loglar = (from l in db.Tbl_Logs
                          join a in db.Tbl_Articles on l.ArticleID equals a.ArticleID
                          select new
                          {
                              ArticleTitle = a.Title,
                              LogDate = l.LogDate,
                              LogDetails = l.LogDetails
                          }).ToList();

            var logDictList = loglar.Select(x => new Dictionary<string, object>
{
                 { "ArticleTitle", x.ArticleTitle },
                 { "LogDate", x.LogDate },
                 { "LogDetails", x.LogDetails }
                 }).ToList();

            ViewBag.Loglar = logDictList;

            var rawList = (from ah in db.Tbl_ArticleHakem
                           join u in db.Tbl_Users on ah.HakemID equals u.UserID
                           join a in db.Tbl_Anonim on ah.ArticleID equals a.AnonimID
                           select new
                           {
                               ID = ah.ID,
                               HakemEmail = u.Email,
                               AnonimTitle = a.AnonimTitle
                           }).ToList(); 
            // Bellekteyken dictionary'ye dönüştür:
            var result = rawList.Select(x => new Dictionary<string, object>{{ "ID", x.ID },{ "HakemEmail", x.HakemEmail },{ "AnonimTitle", x.AnonimTitle } }).ToList();

            ViewBag.DegerlendirmeSonuclari = db.Tbl_Anonim.Where(x => x.AnonimStatus == "Revize İstendi" || x.AnonimStatus == "Tamamlandı").ToList();
            ViewBag.AtanmisHakemler = result;
            ViewBag.YuklenenMakaleler = yuklenenMakaleler;
            ViewBag.AnonimMakaleler = anonimMakaleler;

            return View(); 
        }

        public void LogEkle(int articleId, string mesaj)
        {
            try
            {
                db.Tbl_Logs.Add(new Tbl_Logs
                {
                    ArticleID = articleId,
                    LogDetails = mesaj,
                    LogDate = DateTime.Now
                });
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                throw new Exception("Log eklenirken hata oluştu: " + ex.Message, ex);
            }
        }

        private string RunPythonScript(string pdfPath)
        {
            string pythonPath = @"C:\Users\niltuna\AppData\Local\Programs\Python\Python312\python.exe";
            string scriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "extract_keywords.py");

            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory; // Proje dizini
            string fullPdfPath = System.IO.Path.Combine(baseDirectory, pdfPath.TrimStart('/')); // Tam dosya yolu oluştur
            fullPdfPath = System.IO.Path.GetFullPath(fullPdfPath); // Mutlak yol oluştur

            string arguments = $"\"{scriptPath}\" \"{fullPdfPath}\""; // Python’a tam yolu gönder

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Verb = "runas" // Yönetici yetkisiyle çalıştır
            };


            using (Process process = new Process())
            {
                process.StartInfo = psi;
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return output.Trim();
            }
        }

        [HttpPost]
        public JsonResult GetRecommendedReviewers(int makaleID)
        {
            var makale = db.Tbl_Anonim.Find(makaleID);
            if (makale == null)
                return Json(new { success = false, message = "Makale bulunamadı" });

            string pdfPath = makale.AnonimFilePath;

            Debug.WriteLine("Python'a gönderilen PDF Yolu: " + pdfPath);

            string jsonResponse = RunPythonScript(pdfPath);
            Debug.WriteLine("Python Çıktısı: " + jsonResponse);


            var response = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);

            if (response.ContainsKey("hata"))
            {
                return Json(new { success = false, message = response["hata"].ToString() });
            }

            var matchedKeywords = JsonConvert.DeserializeObject<List<string>>(response["eşleşen_alt_başlıklar"].ToString());

            if (matchedKeywords == null || matchedKeywords.Count == 0)
            {
                Debug.WriteLine("Makale için uygun hakem bulunamadı.");
                return Json(new { success = false, message = "Makale için uygun hakem bulunamadı." });
            }

            var reviewers = db.Tbl_HakemIlgiAlani
                .Where(h => matchedKeywords.Any(k => h.IlgiAlani.ToLower().Contains(k.ToLower())))
                .Select(h => new
                {
                    h.HakemID,
                    UserName = db.Tbl_Users.Where(u => u.UserID == h.HakemID).Select(u => u.Email).FirstOrDefault(),
                    Expertise = h.IlgiAlani
                })
                .ToList();

            return Json(new { success = true, reviewers });
        }

        [HttpPost]
        public JsonResult HakemAta(int MakaleID, int HakemID)
        {
            bool ayniAtamaVarMi = db.Tbl_ArticleHakem.Any(x => x.ArticleID == MakaleID && x.HakemID == HakemID);
            bool baskaHakemeAtanmisMi = db.Tbl_ArticleHakem.Any(x => x.ArticleID == MakaleID);

            if (ayniAtamaVarMi)
            {
                return Json(new { success = false, message = "Bu makale zaten bu hakeme atanmış." });
            }

            if (baskaHakemeAtanmisMi)
            {
                return Json(new { success = false, message = "Bu makale başka bir hakeme atanmış, başka atama yapılamaz." });
            }

            var yeniAtama = new Tbl_ArticleHakem
            {
                ArticleID = MakaleID,
                HakemID = HakemID
            };

            db.Tbl_ArticleHakem.Add(yeniAtama);

            var anonimMakale = db.Tbl_Anonim.FirstOrDefault(a => a.AnonimID == MakaleID);
            if (anonimMakale != null)
            {
                anonimMakale.AnonimStatus = "Hakeme Atandı"; 
            }
            var orijinalMakale = db.Tbl_Articles.FirstOrDefault(a => a.ArticleID == anonimMakale.OrijinalArticleID);
            if (orijinalMakale != null)
            {
                orijinalMakale.Status = "Değerlendiriliyor";
            }
            db.SaveChanges();

            int orijinalID = anonimMakale?.OrijinalArticleID ?? 0;
            if (orijinalID != 0)
            {
                LogEkle(orijinalID, "Makale hakeme atandı.");
            }

            return Json(new { success = true, message = "Hakem başarıyla atandı." });
        }
        [HttpPost]
        public ActionResult HakemIliskisiniSil(int id)
        {
            var atama = db.Tbl_ArticleHakem.Find(id);
            if (atama != null)
            {
                var anonimMakale = db.Tbl_Anonim.FirstOrDefault(a => a.AnonimID == atama.ArticleID);
                if (anonimMakale != null)
                {
                    anonimMakale.AnonimStatus = "Anonim";

                    var orijinalMakale = db.Tbl_Articles.FirstOrDefault(m => m.ArticleID == anonimMakale.OrijinalArticleID);
                    if (orijinalMakale != null)
                    {
                        orijinalMakale.Status = "Yüklenmiş"; 
                    }
                }

                db.Tbl_ArticleHakem.Remove(atama);
                db.SaveChanges();
                LogEkle((int)(anonimMakale.OrijinalArticleID), "Hakem değişikliği için hakem kaldırıldı.");

            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public ActionResult TespitEt(int makaleID)
        {
            var makale = db.Tbl_Articles.Find(makaleID);
            if (makale == null || string.IsNullOrEmpty(makale.FilePath))
            {
                TempData["Error"] = "Makale bulunamadı.";
                return RedirectToAction("Index");
            }

            string apiUrl = "http://127.0.0.1:5000/detect";
            List<dynamic> tespitSonuclari = new List<dynamic>();

            using (var client = new HttpClient())
            using (var form = new MultipartFormDataContent())
            {
                byte[] fileBytes = System.IO.File.ReadAllBytes(Server.MapPath(makale.FilePath));
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/pdf");
                form.Add(fileContent, "pdf", "makale.pdf");

                var response = client.PostAsync(apiUrl, form).Result;

                if (response.IsSuccessStatusCode)
                {
                    var jsonStr = response.Content.ReadAsStringAsync().Result;
                    tespitSonuclari = JsonConvert.DeserializeObject<List<dynamic>>(jsonStr);
                }
                else
                {
                    TempData["Error"] = "Flask API'den veri alınamadı.";
                }
            }
            LogEkle(makaleID, "Makale üzerinde tespit işlemi yapıldı.");
            ViewBag.Makale = makale;
            ViewBag.Tespitler = tespitSonuclari;

            return View("TespitSonuclari");
        }
        [HttpPost]
        public ActionResult Anonimlestir(int makaleID, List<string> types)
        {
            var makale = db.Tbl_Articles.Find(makaleID);
            if (makale == null || string.IsNullOrEmpty(makale.FilePath))
            {
                TempData["Error"] = "Makale bulunamadı.";
                return RedirectToAction("Index");
            }

            string apiUrl = "http://127.0.0.1:5000/anonymize";
            string anonimKlasor = Server.MapPath("/AnonimDosyalar/");
            if (!Directory.Exists(anonimKlasor))
                Directory.CreateDirectory(anonimKlasor);

            string yeniDosyaAdi = $"{makale.Title}_anonim.pdf";
            string kayitYolu = Path.Combine(anonimKlasor, yeniDosyaAdi);

            using (var client = new HttpClient())
            using (var form = new MultipartFormDataContent())
            {
                byte[] fileBytes = System.IO.File.ReadAllBytes(Server.MapPath(makale.FilePath));
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                form.Add(fileContent, "pdf", "makale.pdf");

                foreach (var t in types)
                {
                    form.Add(new StringContent(t), "types");
                }

                var response = client.PostAsync(apiUrl, form).Result;
                if (response.IsSuccessStatusCode)
                {
                    var responseStream = response.Content.ReadAsStreamAsync().Result;
                    using (var fileStream = System.IO.File.Create(kayitYolu))
                    {
                        responseStream.CopyTo(fileStream);
                    }

                    db.Tbl_Anonim.Add(new Tbl_Anonim
                    {
                        OrijinalArticleID = makale.ArticleID,
                        AnonimTitle = makale.Title+"_anonim",
                        AnonimFilePath = "/AnonimDosyalar/" + yeniDosyaAdi,
                        AnonimDate = DateTime.Now,
                        AnonimStatus = "Anonim",
                        AnonimFields=makale.Title
                    });
                    makale.Anonim = true;

                    db.SaveChanges();
                    LogEkle(makaleID, "Makale anonimleştirildi.");
                    TempData["Success"] = "Makale başarıyla anonimleştirildi.";
                }
                else
                {
                    TempData["Error"] = "Anonimleştirme sırasında hata oluştu.";
                }
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public ActionResult YazaraGonder(int makaleID)
        {
            var anonimMakale = db.Tbl_Anonim.FirstOrDefault(m => m.AnonimID == makaleID);
            if (anonimMakale == null || string.IsNullOrEmpty(anonimMakale.AnonimFilePath))
            {
                TempData["Error"] = "Anonim makale bulunamadı.";
                return RedirectToAction("Index");
            }

            string anonimDosyaPath = Server.MapPath(anonimMakale.AnonimFilePath);
            byte[] pdfBytes = System.IO.File.ReadAllBytes(anonimDosyaPath);
            byte[] restoredPdf = null;

            using (var client = new HttpClient())
            using (var content = new MultipartFormDataContent())
            {
                var fileContent = new ByteArrayContent(pdfBytes);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                content.Add(fileContent, "pdf", "anonim.pdf");

                var response = client.PostAsync("http://127.0.0.1:5000/restore", content).Result;

                if (response.IsSuccessStatusCode)
                {
                    restoredPdf = response.Content.ReadAsByteArrayAsync().Result;
                }
                else
                {
                    TempData["Error"] = "Flask'tan geri dönüş alınamadı.";
                    return RedirectToAction("Index");
                }
            }

            var orijinalMakale = db.Tbl_Articles.FirstOrDefault(m => m.ArticleID == anonimMakale.OrijinalArticleID);
            if (orijinalMakale == null || string.IsNullOrEmpty(orijinalMakale.FilePath))
            {
                TempData["Error"] = "Orijinal makale bulunamadı.";
                return RedirectToAction("Index");
            }

            string dosyaAdi = $"{orijinalMakale.Title}_orijinal{orijinalMakale.ArticleID}.pdf";
            string dosyaKlasoru = Path.GetDirectoryName(Server.MapPath(orijinalMakale.FilePath));
            string orijinalPath = Path.Combine(dosyaKlasoru, dosyaAdi);

            // PDF içeriğini orijinal dosya yoluna yaz
            System.IO.File.WriteAllBytes(orijinalPath, restoredPdf);

            orijinalMakale.FilePath = "/" + Path.Combine("MakaleDosyalari", dosyaAdi).Replace("\\", "/");

            orijinalMakale.Status = anonimMakale.AnonimStatus;
            orijinalMakale.SubmissionDate = DateTime.Now;
            orijinalMakale.Anonim = false;
            orijinalMakale.Title = dosyaAdi;

            if (System.IO.File.Exists(anonimDosyaPath))
            {
                System.IO.File.Delete(anonimDosyaPath);
            }

            db.Tbl_Anonim.Remove(anonimMakale);
            db.SaveChanges();
            LogEkle(orijinalMakale.ArticleID, "Makale yazara gönderildi.");

            TempData["Success"] = "Orijinal dosya geri yüklendi, durum güncellendi ve anonim makale silindi.";
            return RedirectToAction("Index");
        }


    }
}