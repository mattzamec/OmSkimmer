using HtmlAgilityPack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
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

      private const String OmSiteRoot = @"https://www.omfoods.com";
      private const String MainNavSectionClassName = "main-nav-bar";
      private const String CategoryClassName = "has-children";
      private const String ProductClassName = "product-item-title";
      private const String ProductDescriptionClassName = "product-description";
      private const String MainProductSectionAttribute = "data-product-container";
      private const String ProductIdAttribute = "data-product-id";
      private const String SizeDivAttribute = "data-product-option-change";
      private const String SingleSizePriceDivClassName = "product-price";
      private const String SingleSizePriceDetailSpanClassName = "price-value";

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
      /// Main method that parses the whole damn thing
      /// </summary>
      public void ParseOmData()
      {
         try
         {
            this.logger.WriteToConsoleAndLogFile("Reading main page ... ");

            HtmlDocument mainPage = this.ReadPage(OmSiteRoot);
            if (mainPage == null)
            {
               return;
            }

            // Get the product nodes. These will be sucked out of the main menu.
            // The main menu is a list (<ul>) of "main" links. All of these except for "Products" are simple links without sub-links.
            // "Products" contains a child list (<ul>) of main category items. Each main category item is a list item (<li>) with a class "has-children".
            // The main category <li> element contains an anchor tag <a> pointing to the main category page, and another nested list (<ul>)
            // whose items contain links to subcategory pages.
            HtmlNode mainNavNode = this.GetSingleDescendantByTypeAndAttribute(mainPage.DocumentNode, "section", "class", MainNavSectionClassName);
            if (mainNavNode == null)
            {
               this.logger.WriteLineToConsoleAndLogFile("Cannot locate main navigation section containing product links; aborting mission.");
               return;
            }

            List<HtmlNode> categoryNodeList = this.GetDescendantListByClassName(mainNavNode, "li", CategoryClassName);
            this.logger.WriteToConsoleAndLogFile("Found {0} category node(s) ", categoryNodeList.Count);
            if (!categoryNodeList.Any())   // If we have no categories, there's nothing we can do ...
            {
               this.logger.BlankLineInConsoleAndLogFile();
               return;
            }

            // Parse out the URLs from all the anchor tags in the category list items
            // This will be a list of string Tuples, where Item1 is the inner text, and Item2 is the URL
            List<Tuple<String, String>> categoryTupleList = this.GetUrlAndNameFromAnchors(categoryNodeList);

            this.logger.WriteLineToConsoleAndLogFile(" with {0} category links", categoryTupleList.Count);
            if (!categoryTupleList.Any())     // If we have no URLs that we could parse out of the categories, there's nothing we can do ...
            {
               this.logger.WriteLineToConsoleAndLogFile("Unable to parse any URLs out of the category node(s).");
               return;
            }

            // Log the category URLs in the log file. No need to write details to console.
            this.logger.BlankLineInLogFile();
            this.logger.WriteLineToLogFile("Category URLs:");
            foreach (Tuple<String, String> categoryTuple in categoryTupleList)
            {
               this.logger.WriteLineToLogFile("{0}: {1}", categoryTuple.Item1, categoryTuple.Item2);
            }
            this.logger.BlankLineInLogFile();

            // Process each category page
            Int32 currentCategoryIndex = 0;
            Int32 productsProcessed = 0;
            List<Product> productList = new List<Product>();
            foreach (Tuple<String, String> categoryTuple in categoryTupleList)
            {
               if (this.numberToProcess > 0 && productsProcessed >= this.numberToProcess)
               {
                  break;
               }

               this.logger.BlankLineInConsole();
               this.logger.WriteToConsoleAndLogFile("Reading {0} page ({1} of {2}) ... ", categoryTuple.Item1, ++currentCategoryIndex, categoryTupleList.Count);

               HtmlDocument categoryPage = this.ReadPage(categoryTuple.Item2);
               if (categoryPage == null)   // Skip failed category URLs
               {
                  continue;
               }

               // Get the product detail nodes. There should be a bunch of them. These are h5s with class attribute containing ProductClassName
               List<HtmlNode> productNodeList = this.GetDescendantListByClassName(categoryPage.DocumentNode, "h5", ProductClassName);

               this.logger.WriteLineToConsoleAndLogFile("Found {0} product node(s) for {1}", productNodeList.Count, categoryTuple.Item1);
               if (!productNodeList.Any())   // There really should be product divs on each category page - but if we can't get them, there's nothing to do ...
               {
                  continue;
               }

               // Parse out the URLs from the anchor tags in all the product divs. There should be one anchor tag per product div.
               List<Tuple<String, String>> productTupleList = this.GetUrlAndNameFromAnchors(productNodeList);
               if (!productTupleList.Any())   // We need to have URLs for product pages to continue
               {
                  this.logger.WriteLineToConsoleAndLogFile("Unable to parse any URLs out for the {0} category.", categoryTuple.Item1);
                  continue;
               }

               // Now we have to hit each product page
               Int32 currentProductIndex = 0;
               foreach (Tuple<String, String> productTuple in productTupleList)
               {
                  this.logger.OverwriteLineToConsole("Processing product {0} of {1} for {2}.",
                      ++currentProductIndex, productTupleList.Count, categoryTuple.Item1);

                  // If this URL was already processed for another category, skip it
                  Product processedProduct = productList.FirstOrDefault(p => p.OmUrl == productTuple.Item2);
                  if (processedProduct != null)
                  {
                     this.logger.WriteLineToLogFile(
                         "SKIPPING {0} ({1}) - THIS WAS ALREADY PROCESSED FOR CATEGORY {2} ... ",
                         productTuple.Item1, productTuple.Item2, processedProduct.Category);
                     continue;
                  }

                  this.logger.WriteToLogFile("Reading {0} page ... ", productTuple.Item1);
                  HtmlDocument productPage = this.ReadPage(productTuple.Item2, true, false);
                  if (productPage == null)    // If we couldn't read the product page, move on, there's nothing we can do
                  {
                     continue;
                  }

                  // Drill down to the product information.
                  // There should be a single section of containing an attribute MainProductSectionAttribute. Let's make sure there is
                  HtmlNode productMainNode = this.GetFirstDescendantByTypeWithAttribute(
                      productPage.DocumentNode, "section", MainProductSectionAttribute);
                  if (productMainNode == null)    // if we can't find the main product div, move on, there's nothing we can do
                  {
                     this.logger.WriteLineToLogFile("Main product section not found.");
                     continue;
                  }

                  // Product description is the HTML content of the appropriate div on the main product page
                  String productDescription = this.GetProductDescription(productPage);

                  // Let's get the product ID from an attribute of the main product section
                  String productIdValue = this.GetAttributeValueByName(productMainNode, ProductIdAttribute);
                  Int32 productId;
                  if (String.IsNullOrEmpty(productIdValue) || !Int32.TryParse(productIdValue, out productId))
                  {
                     this.logger.WriteLineToLogFile("Unable to parse product ID from the page contents.");
                     continue;
                  }

                  // We need to get in-stock status from the main product information; it seems the JSON returned by AJAX
                  // for the different sizes is not very reliable. To get this, we'll find a JavaScript snippet that appears
                  // near the top of every page containing product information
                  HtmlNode bcDataJsNode = this.GetDescendantListByTypeAndAttribute(productPage.DocumentNode,
                      "script", "type", @"text/javascript").
                      FirstOrDefault(
                          node =>
                              node.InnerText.Trim().StartsWith("var BCData = {\"product_attributes\":",
                                  StringComparison.OrdinalIgnoreCase));
                  Boolean? isProductInStock = null;
                  if (bcDataJsNode != null)
                  {
                     OmProduct mainOmProduct;
                     try
                     {
                        mainOmProduct =
                            JsonConvert.DeserializeObject<OmProduct>(
                                bcDataJsNode.InnerText.Trim()
                                    .Substring("var BCData = ".Length)
                                    .Replace("product_attributes", "data").TrimEnd(';'));
                     }
                     catch
                     {
                        mainOmProduct = null;
                     }
                     if (mainOmProduct != null)
                     {
                        isProductInStock = mainOmProduct.data.purchasable;
                     }
                  }

                  // OK. Now let's see if there are any radio buttons for different sizes
                  HtmlNode sizeRadioMainDivNode = this.GetSingleDescendantByTypeWithAttribute(productMainNode, "div", SizeDivAttribute);
                  List<HtmlNode> sizeRadioNodeList = sizeRadioMainDivNode == null ? new List<HtmlNode>() :
                      this.GetDescendantListByTypeAndAttribute(sizeRadioMainDivNode, "input", "type", "radio", true);

                  // If we have size radio buttons, we'll hit the remote.php AJAX page with the appropriate arguments and parse each price and availability from JSON response.
                  // If there are not, we'll parse the price and availability from the page and that's all we got
                  if (sizeRadioNodeList.Any())
                  {
                     Boolean firstSize = true;     // We'll only retrieve the description for the first size of the product since it will be the same for all
                     foreach (HtmlNode radioNode in sizeRadioNodeList)
                     {
                        String jsonResult = String.Empty;
                        WebClient client = new WebClient();

                        try
                        {
                           Byte[] response = client.UploadValues(String.Format(@"http://www.omfoods.com/remote/v1/product-attributes/{0}", productId),
                               new NameValueCollection()
                               {
                                            {"action", "add"},
                                            {"product_id", productId.ToString(CultureInfo.InvariantCulture)},
                                            {this.GetAttributeValueByName(radioNode, "name"), this.GetAttributeValueByName(radioNode, "value")},
                                            {"qty[]", "1"}
                               });

                           jsonResult = System.Text.Encoding.UTF8.GetString(response);
                        }
                        catch (Exception ex)
                        {
                           this.logger.WriteLineToLogFile("ERROR GETTING AJAX DATA FOR PRODUCT ID {0}: {1}", productId, ex.Message);
                        }
                        finally
                        {
                           client.Dispose();
                        }

                        if (String.IsNullOrEmpty(jsonResult))
                        {
                           this.logger.WriteLineToLogFile("RETRIEVED NO AJAX DATA FOR PRODUCT ID {0}", productId);
                           continue;
                        }

                        OmProduct omProduct = JsonConvert.DeserializeObject<OmProduct>(jsonResult);

                        Product product = new Product
                        {
                           Name = productTuple.Item1,
                           Description = firstSize ? productDescription : String.Empty,
                           Category = categoryTuple.Item1,
                           OmId = productId,
                           OmUrl = productTuple.Item2,
                           Size = this.GetSizeDescriptionFromRadioButton(radioNode.ParentNode),
                           OmPrice = omProduct.data.price.without_tax.value,
                           Price = Math.Round(omProduct.data.price.without_tax.value / 0.75m, 3),
                           VariantId = omProduct.data.variantId,
                           IsInStock = isProductInStock ?? omProduct.data.purchasable
                        };

                        productList.Add(product);
                        firstSize = false;
                     }
                  }
                  else    // There are no size radio buttons, so we only have a single product to worry about
                  {
                     Decimal price;

                     // There is a price div that should give us information about the price; this div also seems to contain an "Out of stock" message 
                     // if a product is out of stock. Let's see if we can get it.
                     HtmlNode priceDiv = this.GetSingleDescendantByTypeAndAttribute(productMainNode, "div",
                         "class", SingleSizePriceDivClassName, true);
                     if (priceDiv == null)
                     {
                        this.logger.WriteLineToLogFile("CANNOT FIND {0} PRICE DIV IN THE MAIN PRODUCT ELEMENT", SingleSizePriceDivClassName);
                        continue;
                     }

                     // If the price div contains a <p> element that starts with "Out of stock", we can set the price to 0.00,
                     // otherwise we'll try to parse the price out of a child div
                     HtmlNode outOfStockParagraph = priceDiv.Element("p");
                     if (outOfStockParagraph != null &&
                         outOfStockParagraph.InnerText.ToLower().Contains("out of stock"))
                     {
                        price = 0.00m;
                     }
                     else
                     {
                        HtmlNode priceDetailSpan = this.GetSingleDescendantByTypeAndAttribute(priceDiv, "span",
                            "class", SingleSizePriceDetailSpanClassName);
                        if (priceDetailSpan == null)
                        {
                           this.logger.WriteLineToLogFile("CANNOT FIND {0} PRICE DETAIL SPAN IN THE MAIN PRICE DIV", SingleSizePriceDetailSpanClassName);
                           continue;
                        }

                        if (!Decimal.TryParse(priceDetailSpan.InnerText.Trim().TrimStart('$'), out price))
                        {
                           price = 0.00m;
                        }
                     }

                     Product product = new Product
                     {
                        Name = productTuple.Item1,
                        Description = productDescription,
                        Category = categoryTuple.Item1,
                        OmId = productId,
                        OmUrl = productTuple.Item2,
                        OmPrice = price,
                        Price = Math.Round(price / 0.75m, 3),
                        IsInStock = price > 0.00m
                     };

                     // product.Size and product.VariantId are going to be empty if this is the only size available

                     productList.Add(product);
                  }

                  if (this.numberToProcess > 0 && ++productsProcessed >= this.numberToProcess)
                  {
                     break;
                  }
               }
            }

            this.logger.WriteProductInfo(productList);
         }
         catch (Exception ex)
         {
            this.logger.BlankLineInConsoleAndLogFile();
            this.logger.BlankLineInConsoleAndLogFile();
            this.logger.WriteLineToConsoleAndLogFile("PARSING ERROR: {0}", ex.Message);
         }
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
            WebRequest r = WebRequest.Create(url);
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

      /// <summary>
      /// Gets all descendants of the given node that have the given type and attribute value
      /// </summary>
      /// <param name="parentNode">Parent node</param>
      /// <param name="descendantType">HTML tag type to look for</param>
      /// <param name="attributeName">Attribute name to look for</param>
      /// <param name="attributeValue">Attribute value to look for</param>
      /// <param name="strictComparison">Optional value to match the attribute value exactly</param>
      /// <returns>List of matching node descendants</returns>
      private List<HtmlNode> GetDescendantListByTypeAndAttribute(HtmlNode parentNode, String descendantType, String attributeName, String attributeValue, Boolean strictComparison = false)
      {
         return parentNode.Descendants(descendantType).Where(d => d.Attributes.Contains(attributeName)
             && (strictComparison ? d.Attributes[attributeName].Value.Equals(attributeValue, StringComparison.Ordinal)
                 : d.Attributes[attributeName].Value.Contains(attributeValue))).ToList();
      }

      /// <summary>
      /// Gets a single descendant of the given node that has the given type and attribute values.
      /// </summary>
      /// <param name="parentNode">Parent node</param>
      /// <param name="descendantType">HTML tag type to look for</param>
      /// <param name="attributeName">Attribute name to look for</param>
      /// <param name="attributeValue">Attribute value to look for</param>
      /// <param name="strictComparison">Optional value to match the attribute value exactly</param>
      /// <returns>Single descendant of the given node matching the supplied values. If a descendant is not found, or if more than one are found, returns null</returns>
      private HtmlNode GetSingleDescendantByTypeAndAttribute(HtmlNode parentNode, String descendantType, String attributeName, String attributeValue, Boolean strictComparison = false)
      {
         try
         {
            return
                this.GetDescendantListByTypeAndAttribute(parentNode, descendantType, attributeName, attributeValue,
                    strictComparison).Single();
         }
         catch
         {
            return null;
         }
      }

      /// <summary>
      /// Gets a single descendant of the given node that has the given type and contains the given attribute, regardless of the attribute's value.
      /// </summary>
      /// <param name="parentNode">Parent node</param>
      /// <param name="descendantType">HTML tag type to look for</param>
      /// <param name="attributeName">Attribute name to look for</param>
      /// <returns>Single descendant of the given node matching the supplied values. If a descendant is not found, or if more than one are found, returns null</returns>
      private HtmlNode GetSingleDescendantByTypeWithAttribute(HtmlNode parentNode, String descendantType, String attributeName)
      {
         try
         {
            return parentNode.Descendants(descendantType).Single(d => d.Attributes.Contains(attributeName));
         }
         catch
         {
            return null;
         }
      }

      /// <summary>
      /// Gets the first descendant of the given node that has the given type and contains the given attribute, regardless of the attribute's value.
      /// </summary>
      /// <param name="parentNode">Parent node</param>
      /// <param name="descendantType">HTML tag type to look for</param>
      /// <param name="attributeName">Attribute name to look for</param>
      /// <returns>First descendant of the given node matching the supplied values. If a descendant is not found, returns null</returns>
      private HtmlNode GetFirstDescendantByTypeWithAttribute(HtmlNode parentNode, String descendantType, String attributeName)
      {
         try
         {
            return parentNode.Descendants(descendantType).FirstOrDefault(d => d.Attributes.Contains(attributeName));
         }
         catch
         {
            return null;
         }
      }

      /// <summary>
      /// Gets all descendants of the given node that have the given type and class name
      /// </summary>
      /// <param name="parentNode">Parent node</param>
      /// <param name="descendantType">HTML tag type to look for</param>
      /// <param name="className">Class name to look for</param>
      /// <param name="strictComparison">Optional value to match the attribute value exactly</param>
      /// <returns>List of matching node descendants</returns>
      private List<HtmlNode> GetDescendantListByClassName(HtmlNode parentNode, String descendantType, String className, Boolean strictComparison = false)
      {
         return this.GetDescendantListByTypeAndAttribute(parentNode, descendantType, "class", className, strictComparison);
      }

      /// <summary>
      /// Gets the value of an attribute of a node by attribute name
      /// </summary>
      /// <param name="node">HtmlNode</param>
      /// <param name="attributeName">Attribute name</param>
      /// <returns>Value of the attribute by name</returns>
      private String GetAttributeValueByName(HtmlNode node, String attributeName)
      {
         return node != null && node.Attributes.Contains(attributeName) ? node.Attributes[attributeName].Value : String.Empty;
      }

      /// <summary>
      /// Takes a list of HtmlNodes, each of which should contain some anchor tags. Parses out information from all anchor tags in all nodes in the list,
      /// putting the inner text in the first item and the href URL in the other.
      /// </summary>
      /// <param name="nodeList">List of HtmlNodes to parse</param>
      /// <returns>A list of String, String tuples with each anchor's inner text as the first item and the href URL in the other.</returns>
      private List<Tuple<String, String>> GetUrlAndNameFromAnchors(IEnumerable<HtmlNode> nodeList)
      {
         List<Tuple<String, String>> tupleList = new List<Tuple<String, String>>();
         foreach (HtmlNode categoryNode in nodeList)
         {
            foreach (HtmlNode anchorTag in categoryNode.Descendants("a"))
            {
               String link = anchorTag.Attributes["href"].Value;
               if (!tupleList.Any(tpl => tpl.Item2.Equals(link)))
               {
                  tupleList.Add(Tuple.Create(anchorTag.InnerText.Replace("&amp;", "&").Replace('•', '&'), link));
               }
            }
         }
         tupleList.Reverse();
         return tupleList;
      }

      /// <summary>
      /// Parses the product description - including HTML markup - from the main product page
      /// </summary>
      /// <param name="productPage">Entire product page document</param>
      /// <returns>Product description parsed from the page/div supplied</returns>
      private String GetProductDescription(HtmlDocument productPage)
      {
         HtmlNode descriptionNode = this.GetSingleDescendantByTypeAndAttribute(productPage.DocumentNode, "div",
             "class", ProductDescriptionClassName);

         // Anchor tag removal adapted from https://stackoverflow.com/questions/12787449/html-agility-pack-removing-unwanted-tags-without-removing-content
         if (descriptionNode == null)
         {
            return String.Empty;
         }

         HtmlNodeCollection tryGetNodes = descriptionNode.SelectNodes("./*|./text()");

         if (tryGetNodes == null || !tryGetNodes.Any())
         {
            return descriptionNode.InnerHtml;
         }

         Queue<HtmlNode> nodes = new Queue<HtmlNode>(tryGetNodes);

         while (nodes.Count > 0)
         {
            HtmlNode node = nodes.Dequeue();
            HtmlNode parentNode = node.ParentNode;

            HtmlNodeCollection childNodes = node.SelectNodes("./*|./text()");

            if (childNodes != null)
            {
               foreach (HtmlNode child in childNodes)
               {
                  nodes.Enqueue(child);
               }
            }

            if (node.Name.Equals("a", StringComparison.InvariantCultureIgnoreCase))
            {
               if (childNodes != null)
               {
                  foreach (HtmlNode child in childNodes)
                  {
                     parentNode.InsertBefore(child, node);
                  }
               }

               parentNode.RemoveChild(node);
            }
         }

         return descriptionNode.InnerHtml;
      }

      /// <summary>
      /// Gets the size description from a label containing it
      /// </summary>
      /// <param name="labelNode">Label HtmlNode</param>
      /// <returns>Size description</returns>
      private String GetSizeDescriptionFromRadioButton(HtmlNode labelNode)
      {
         if (labelNode == null)
         {
            return String.Empty;
         }

         HtmlNode spanSizeName = this.GetDescendantListByClassName(labelNode, "span", "form-label-text", true).FirstOrDefault();
         return spanSizeName == null ? String.Empty : spanSizeName.InnerText;
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
