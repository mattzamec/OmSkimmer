using HtmlAgilityPack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace OmSkimmer
{
   public class HtmlParser : IDisposable
   {
      #region Members

      private Boolean disposed = false;
      private readonly Int32 numberToProcess = 0;
      private readonly Logger logger;

      #endregion Members

      #region Constructors

      /// <summary>
      /// Constructor accepting processing options and number of products to process
      /// </summary>
      /// <param name="outputOptions">Output options</param>
      /// <param name="processThisMany">Number of products to process (useful for testing purposes)</param>
      public HtmlParser(Shared.Options outputOptions, Int32 processThisMany = 0)
      {
         this.numberToProcess = processThisMany;
         this.logger = new Logger(outputOptions);
      }

      #endregion Constructors

      #region ParseOmData

      /// <summary>
      /// Parses product information from a page of data.
      /// </summary>
      /// <param name="page">HtmlDocument containing a page of product information</param>
      /// <returns>List of Product objects; if exception was encountered or page is empty, returns an empty list, never null.</returns>
      private List<Product> ParseProducts(HtmlDocument page)
      {
         if (page == null)
         {
            this.logger.WriteLineToConsoleAndLogFile("ERROR: PAGE IS NULL ... ");
            return new List<Product>();
         }

         // Product information for each product on a result page is contained in a segment like this:
         // <script>theme.productData[{product bigint ID}] = { JSON product info }</script>
         // So we need to try and parse out these tags to get all the product info we need.
         return page.DocumentNode.Descendants("script")
            .Where(x => x.InnerHtml.StartsWith("theme.productData[", StringComparison.InvariantCultureIgnoreCase))
            .Select(x => JsonConvert.DeserializeObject<OmProduct>(x.InnerHtml.Substring(x.InnerHtml.IndexOf('=') + 1).Trim().TrimEnd(';')))
            .SelectMany(omProduct => Product.ParseListFromOmProduct(omProduct)).ToList();
      }

      /// <summary>
      /// Main method that parses the whole damn thing
      /// </summary>
      public void ParseOmData()
      {
         // Read the collections/all?page=x paged results until there are no more.
         // Parse the product info on each page and compose a master list of product info.
         var pageNumber = 1;
         var productList = new List<Product>();
         this.logger.BlankLineInConsole();
         while (true)
         {
            this.logger.WriteToConsoleAndLogFile("Reading product page {0} ... ", pageNumber);
            var productsOnPage = this.ParseProducts(this.ReadPage($"https://www.omfoods.com/collections/all?page={pageNumber++}"));
            this.logger.WriteLineToConsoleAndLogFile("Parsed out {0} products ... ", productsOnPage.Count);

            if (!productsOnPage.Any() || (this.numberToProcess > 0 && productList.Count > this.numberToProcess))
               break;

            productList.AddRange(productsOnPage);
         }

         this.logger.WriteProductInfo(productList);
      }

      #endregion ParseOmData

      #region Helper Methods

      /// <summary>
      /// Reads a page into an HtmlDocument
      /// </summary>
      /// <param name="url">Page URL</param>
      /// <param name="printOk">Optional boolean to suppress printing "OK" to the console when page is successfully parsed.</param>
      /// <param name="includeConsoleOutput">Include console output</param>
      /// <returns>HtmlDocument parsed from the given URL</returns>
      private HtmlDocument ReadPage(String url, Boolean printOk = true, Boolean includeConsoleOutput = true)
      {
         HtmlDocument html = new HtmlDocument();
         String message = printOk ? "OK" : String.Empty;

         try
         {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.ServerCertificateValidationCallback = (s, cert, chain, ssl) => true;
            var r = (HttpWebRequest)WebRequest.Create(url);
            r.UserAgent = "Mozilla / 5.0(Windows NT 10.0; Win64; x64; rv: 62.0) Gecko / 20100101 Firefox / 62.0";
            WebResponse resp = r.GetResponse();
            using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
            {
               html.LoadHtml(sr.ReadToEnd());
            }
         }
         catch (Exception ex)
         {
            message = String.Format("ERROR: {0}", ex.Message);
         }

         if (html == null || String.IsNullOrEmpty(html.ParsedText))
         {
            message = String.Format("Failed to retrieve anything from {0}.", url);
         }

         if (!String.IsNullOrEmpty(message))
         {
            if (includeConsoleOutput)
            {
               this.logger.WriteLineToConsoleAndLogFile(message);
            }
            else
            {
               this.logger.WriteLineToLogFile(message);
            }
         }

         return html;
      }

      #endregion Helper Methods

      #region IDisposable members

      private void Dispose(bool disposing)
      {
         if (!disposed)
         {
            if (disposing)
            {
               if (this.logger != null)
               {
                  this.logger.Dispose();
               }
            }

            disposed = true;
         }
      }

      public void Dispose()
      {
         this.Dispose(true);
      }

      #endregion IDisposable members
   }
}
