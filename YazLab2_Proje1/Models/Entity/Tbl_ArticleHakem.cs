//------------------------------------------------------------------------------
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
    using System.Collections.Generic;
    
    public partial class Tbl_ArticleHakem
    {
        public int ID { get; set; }
        public Nullable<int> ArticleID { get; set; }
        public Nullable<int> HakemID { get; set; }
    
        public virtual Tbl_Users Tbl_Users { get; set; }
        public virtual Tbl_Anonim Tbl_Anonim { get; set; }
    }
}
