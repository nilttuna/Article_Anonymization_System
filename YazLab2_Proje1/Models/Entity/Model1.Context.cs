﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace YazLab2_Proje1.Models.Entity
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    
    public partial class AnonimlestirmeSistemiEntities : DbContext
    {
        public AnonimlestirmeSistemiEntities()
            : base("name=AnonimlestirmeSistemiEntities")
        {
        }
    
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            throw new UnintentionalCodeFirstException();
        }
    
        public virtual DbSet<sysdiagrams> sysdiagrams { get; set; }
        public virtual DbSet<Tbl_ArticleHakem> Tbl_ArticleHakem { get; set; }
        public virtual DbSet<Tbl_Articles> Tbl_Articles { get; set; }
        public virtual DbSet<Tbl_HakemIlgiAlani> Tbl_HakemIlgiAlani { get; set; }
        public virtual DbSet<Tbl_Logs> Tbl_Logs { get; set; }
        public virtual DbSet<Tbl_Messages> Tbl_Messages { get; set; }
        public virtual DbSet<Tbl_Reviews> Tbl_Reviews { get; set; }
        public virtual DbSet<Tbl_Users> Tbl_Users { get; set; }
        public virtual DbSet<Tbl_Anonim> Tbl_Anonim { get; set; }
    }
}
