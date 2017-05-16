using System;
using Newtonsoft.Json;

namespace OmSkimmer
{
    public class OmProduct
    {
        #region Properties

        public OmProductData data { get; set; }

        #endregion Properties

        #region Constructor

        public OmProduct()
        {
            this.data = new OmProductData();
        }

        #endregion Constructor
    }

    public class OmProductData
    {
// {
//  "data": {
//      "purchasable":false,
//      "purchasing_message":"Out of Stock",
//      "sku":null,
//      "upc":null,
//      "stock":null,
//      "instock":true,
//      "stock_message":null,
//      "price":{
//          "without_tax":{
//              "formatted":"$327.50",
//              "value":327.5
//          },
//          "tax_label":"Tax"
//      },
//      "weight":null,
//      "base":false,
//      "image":null,
//      "variantId":1230
//  }
// }

        #region Properties

        public Boolean purchasable { get; set; }
        public String purchasing_message { get; set; }
        public String sku { get; set; }
        public String upc { get; set; }
        public String stock { get; set; }
        public Boolean instock { get; set; }
        public String stock_message { get; set; }
        public Int32 variantId { get; set; }
        public OmPrice price { get; set; }

        #endregion Properties

        #region Constructor

        public OmProductData()
        {
            this.purchasable = false;
            this.purchasing_message = String.Empty;
            this.sku = String.Empty;
            this.upc = String.Empty;
            this.stock = String.Empty;
            this.instock = false;
            this.stock_message = String.Empty;
            this.variantId = -1;
            this.price = null;
        }

        #endregion Constructor
    }

    public class OmPrice
    {
        //      "price":{
        //          "without_tax":{
        //              "formatted":"$327.50",
        //              "value":327.5
        //          },
        //          "tax_label":"Tax"
        //      },
        #region Properties

        public OmPriceDetails without_tax { get; set; }
        public String tax_label { get; set; }

        #endregion Properties

        #region Constructor

        public OmPrice()
        {
            this.without_tax = null;
            this.tax_label = String.Empty;
        }

        #endregion Constructor        
    }

    public class OmPriceDetails
    {
        //      "price":{
        //          "without_tax":{
        //              "formatted":"$327.50",
        //              "value":327.5
        //          },
        //          "tax_label":"Tax"
        //      },
        #region Properties

        public String formatted { get; set; }
        public Decimal value { get; set; }

        #endregion Properties

        #region Constructor

        public OmPriceDetails()
        {
            this.formatted = String.Empty;
            this.value = 0.00m;
        }

        #endregion Constructor
    }
}
