using System;

namespace OmSkimmer
{
    public class Product
    {
        #region Properties

        public String Name { get; set; }
        public String Description { get; set; }
        public String Category { get; set; }
        public String Size { get; set; }
        public Int32 OmId { get; set; }
        public String OmUrl { get; set; }
        public Int32 VariantId { get; set; }
        public Decimal Price { get; set; }
        public Decimal OmPrice { get; set; }
        public Boolean IsInStock { get; set; }

        /// <summary>
        /// Product detail used for logging
        /// </summary>
        public String Detail
        {
            get
            {
                return String.Format("ID: {0}, Variant ID: {1}, Name: {2}, Category: {3}, Size: {4}, OM Price: {5:C}, Price: {6:C}, {7}",
                    this.OmId, this.VariantId, this.Name, this.Category, this.Size, this.OmPrice, this.Price, this.IsInStock ? "In stock" : "OUT OF STOCK");
            }
        }

        #endregion Properties

        #region Constructor

        public Product()
        {
            this.Name = String.Empty;
            this.Description = String.Empty;
            this.Category = String.Empty;
            this.Size = String.Empty;
            this.OmId = -1;
            this.OmUrl = String.Empty;
            this.VariantId = -1;
            this.Price = 0.00m;
            this.OmPrice = 0.00m;
            this.IsInStock = false;
        }

        #endregion Constructor
    }
}
