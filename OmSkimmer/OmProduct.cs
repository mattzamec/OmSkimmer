namespace OmSkimmer
{
   public class OmProduct
   {
      #region Properties

      public long id { get; set; }
      public string description { get; set; }
      public string type { get; set; }    // category
      public OmProductVariant[] variants { get; set; }

      #endregion Properties
    }

   public class OmProductVariant
   {
      #region Properties

      public long id { get; set; }
      public bool available { get; set; }
      public string name { get; set; }
      public int price { get; set; }      // integer price in cents
      public string title { get; set; }   // pricing unit

      #endregion Properties
   }
}
