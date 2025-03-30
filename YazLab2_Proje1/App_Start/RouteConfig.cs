using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using YazLab2_Proje1.Models.Entity;

namespace YazLab2_Proje1
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            // SQL'den Hakem ID'lerini al
            List<int> hakemIdleri = GetHakemIds();

            // Her bir hakem için özel rota oluştur
            foreach (var hakemId in hakemIdleri)
            {
                routes.MapRoute(
                    name: "Hakem" + hakemId,
                    url: "Hakem" + hakemId,
                    defaults: new { controller = "Hakem", action = "Index", hakemId = hakemId }
                );
            }
            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }

        private static List<int> GetHakemIds()
        {
            using (AnonimlestirmeSistemiEntities db = new AnonimlestirmeSistemiEntities()) // DB Context
            {
                return db.Tbl_Users
                         .Where(u => u.Role == "Hakem") // Hakem olan kullanıcıları al
                         .Select(u => u.UserID) // Eğer PK sütunu UserID ise
                         .ToList();
            }
        }
    }
}
