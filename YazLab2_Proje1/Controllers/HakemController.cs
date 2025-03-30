using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using YazLab2_Proje1.Models.Entity;

namespace YazLab2_Proje1.Controllers
{
    public class HakemController : Controller
    {
        // GET: Hakem
        AnonimlestirmeSistemiEntities db = new AnonimlestirmeSistemiEntities();
        public ActionResult Index(int hakemId)
        {
            Session["HakemID"] = hakemId;
            ViewBag.HakemID = hakemId;
            var anonimMakaleler = db.Tbl_ArticleHakem
            .Where(ah => ah.HakemID == hakemId)
            .Join(db.Tbl_Anonim,
             ah => ah.ArticleID,
             a => a.AnonimID,
            (ah, a) => a) 
            .ToList();

            ViewBag.AnonimMakaleler = anonimMakaleler;
            return View();
        }
        [HttpPost]
        public ActionResult YorumKaydet(int MakaleId, int HakemID, List<string> Yorumlar)
        {
            if (Yorumlar == null || !Yorumlar.Any())
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            var makale = db.Tbl_Anonim.FirstOrDefault(m => m.AnonimID == MakaleId);
            if (makale == null)
                return HttpNotFound("Makale bulunamadı.");

            string pdfTamYolu = Server.MapPath(makale.AnonimFilePath);

            // PDF Kaydetme işlemi
            bool pdfSonuc = PdfKaydet(pdfTamYolu, HakemID, Yorumlar);
            if (!pdfSonuc)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError);

            foreach (var yorum in Yorumlar)
            {
                var review = new Tbl_Reviews
                {
                    ArticleID = MakaleId,
                    ReviewerID = HakemID,
                    Comment = yorum,
                    ReviewDate = DateTime.Now
                };

                db.Tbl_Reviews.Add(review);
            }

            db.SaveChanges();
            return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
        }

        private bool PdfKaydet(string pdfPath, int hakemId, List<string> yorumlar)
        {
            try
            {
                byte[] yeniPdfBytes;

                using (MemoryStream ms = new MemoryStream())
                {
                    PdfReader reader = new PdfReader(pdfPath);
                    Document document = new Document();
                    PdfCopy copy = new PdfCopy(document, ms);
                    document.Open();

                    // Mevcut sayfaları yeni PDF'e ekle
                    for (int page = 1; page <= reader.NumberOfPages; page++)
                    {
                        copy.AddPage(copy.GetImportedPage(reader, page));
                    }

                    // Yeni yorum sayfasını oluştur
                    using (MemoryStream yorumStream = new MemoryStream())
                    {
                        Document yorumDoc = new Document(PageSize.A4);
                        PdfWriter.GetInstance(yorumDoc, yorumStream);
                        yorumDoc.Open();

                        var bf = BaseFont.CreateFont(BaseFont.HELVETICA, "windows-1254", BaseFont.NOT_EMBEDDED);
                        Font font = new Font(bf, 12);

                        yorumDoc.Add(new Paragraph("Makale Yorumları", font));
                        yorumDoc.Add(new Paragraph($"Hakem ID: {hakemId}", font));
                        yorumDoc.Add(new Paragraph($"Tarih: {DateTime.Now}", font));
                        yorumDoc.Add(new Paragraph(" ", font));

                        int yorumNo = 1;
                        foreach (var yorum in yorumlar)
                        {
                            yorumDoc.Add(new Paragraph($"{yorumNo++}. Yorum: {yorum}", font));
                            yorumDoc.Add(new Paragraph(" ", font));
                        }

                        yorumDoc.Close();

                        PdfReader yorumReader = new PdfReader(yorumStream.ToArray());
                        copy.AddPage(copy.GetImportedPage(yorumReader, 1));
                    }

                    document.Close();
                    reader.Close();

                    yeniPdfBytes = ms.ToArray();
                }

                System.IO.File.WriteAllBytes(pdfPath, yeniPdfBytes);

                return true;
            }
            catch
            {
                return false;
            }
        }
        [HttpPost]
        public ActionResult DurumGuncelle(int? MakaleId, string Durum)
        {
            if (MakaleId == null)
            {
                TempData["Mesaj"] = "Lütfen bir makale seçiniz.";
                return RedirectToAction("Index", new { hakemId = (int)Session["HakemID"] });
            }

            var makale = db.Tbl_Anonim.FirstOrDefault(m => m.AnonimID == MakaleId);
            if (makale == null)
            {
                TempData["Mesaj"] = "Makale bulunamadı.";
                return RedirectToAction("Index", new { hakemId = (int)Session["HakemID"] });
            }

            makale.AnonimStatus = Durum;
            db.SaveChanges();

            int hakemId = (int)Session["HakemID"];
            var atama = db.Tbl_ArticleHakem
                         .FirstOrDefault(ah => ah.ArticleID == MakaleId && ah.HakemID == hakemId);

            if (atama != null)
            {
                db.Tbl_ArticleHakem.Remove(atama);
            }

            db.SaveChanges();
            int orijinalID =makale?.OrijinalArticleID ?? 0;
            if (orijinalID != 0)
            {
                LogEkle(orijinalID, Durum);
            }
            TempData["Mesaj"] = $"Makale durumu '{Durum}' olarak güncellendi ve hakemden kaldırıldı.";
            return RedirectToAction("Index", new { hakemId = hakemId });
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







    }
}