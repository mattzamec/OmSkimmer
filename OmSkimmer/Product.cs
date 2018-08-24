using System;
using System.Collections.Generic;

namespace OmSkimmer
{
   public class Product
   {
      #region Properties

      public String Name { get; set; }
      public String Description { get; set; }
      public String Category { get; set; }
      public String Size { get; set; }
      public Int64 OmId { get; set; }
      public Int64 VariantId { get; set; }
      public Decimal Price { get; set; }
      public Boolean IsInStock { get; set; }

      #endregion Properties

      /// <summary>
      /// Product detail used for logging
      /// </summary>
      public String Detail
      {
         get
         {
            return String.Format("ID: {0}, Variant ID: {1}, Name: {2}, Category: {3}, Size: {4}, Price: {5:C}, {6}",
                this.OmId, this.VariantId, this.Name, this.Category, this.Size, this.Price, this.IsInStock ? "In stock" : "OUT OF STOCK");
         }
      }

      #region Constructor

      public Product()
      {
         this.Name = String.Empty;
         this.Description = String.Empty;
         this.Category = String.Empty;
         this.Size = String.Empty;
         this.OmId = -1;
         this.VariantId = -1;
         this.Price = 0.00m;
         this.IsInStock = false;
      }

      public static List<Product> ParseListFromOmProduct(OmProduct omProduct)
      {
         List<Product> resultList = new List<Product>();
         bool first = true;
         foreach (OmProductVariant variant in omProduct.variants)
         {
            resultList.Add(new Product
            {
               Name = variant.name,
               Description = first ? omProduct.description : string.Empty,
               Category = omProduct.type,
               Size = variant.title,
               OmId = omProduct.id,
               VariantId = variant.id,
               Price = (variant.price / 100.00m) / 0.75m,
               IsInStock = variant.available
            });

            first = false;
         }
         return resultList;
      }

      #endregion Constructor
   }
}
