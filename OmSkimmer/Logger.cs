using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;

namespace OmSkimmer
{
    public class Logger : IDisposable
    {
        #region Members

        private Boolean disposed = false;
        private readonly Shared.Options options;
        private readonly DateTime startDate;

        private String outputRoot;
        private String OutputRoot
        {
            get
            {
                if (String.IsNullOrEmpty(this.outputRoot))
                {
                    const String debugFolder = @"\OmSkimmer\bin\Debug\";
                    this.outputRoot = AppDomain.CurrentDomain.BaseDirectory;
                    if (this.outputRoot.EndsWith(debugFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        this.outputRoot = this.outputRoot.Substring(0,
                            this.outputRoot.IndexOf(debugFolder, StringComparison.OrdinalIgnoreCase));
                    }
                    this.outputRoot = Path.Combine(this.outputRoot, DateTime.Today.ToString("yyyy_MM_dd"));

                    if (!Directory.Exists(this.outputRoot))
                    {
                        Directory.CreateDirectory(this.outputRoot);
                    }
                }

                return this.outputRoot;
            }
        }

        private String logFileName;
        private String LogFileName
        {
            get
            {
                if (String.IsNullOrEmpty(this.logFileName))
                {
                    String logFolder = Path.Combine(this.OutputRoot, "Logs");
                    if (!Directory.Exists(logFolder))
                    {
                        Directory.CreateDirectory(logFolder);
                    }

                    this.logFileName = Path.Combine(logFolder,
                        String.Format("log_{0}.txt", DateTime.Now.ToString("HH_mm_ss")));
                }

                return this.logFileName;
            }
        }
        
        private StreamWriter logStream;
        private StreamWriter LogStream
        {
            get
            {
                return this.logStream ?? (this.logStream = File.AppendText(this.LogFileName));
            }
        }

        private String CsvFileName
        {
            get
            {
                return Path.Combine(this.OutputRoot, "OmPriceList.csv");
            }
        }

        private StreamWriter csvStream;
        private StreamWriter CsvStream
        {
            get
            {
                return this.csvStream ?? (this.csvStream = File.CreateText(this.CsvFileName));
            }
        }

        private String SqlFileName
        {
            get
            {
                return Path.Combine(this.OutputRoot, "OmProducts.sql");
            }
        }

        private StreamWriter sqlStream;
        private StreamWriter SqlStream
        {
            get
            {
                return this.sqlStream ?? (this.sqlStream = File.CreateText(this.SqlFileName));
            }
        }

        #endregion Members

        #region Constructors

        /// <summary>
        /// Constructor accepts output options
        /// </summary>
        /// <param name="outputOptions">Output options</param>
        public Logger(Shared.Options outputOptions)
        {
            this.startDate = DateTime.Now;
            this.options = outputOptions;
            this.WriteLineToLogFile("********** LOGGING STARTED: {0} **************", this.startDate.ToString("F"));
        }

        #endregion Constructors

        #region Methods

        public void WriteToConsole(String message, params Object[] formatArgs)
        {
            Console.Write(formatArgs.Length > 0 ? String.Format(message, formatArgs) : message);
        }

        public void WriteLineToConsole(String message, params Object[] formatArgs)
        {
            Console.WriteLine(formatArgs.Length > 0 ? String.Format(message, formatArgs) : message);
        }

        public void OverwriteLineToConsole(String message, params Object[] formatArgs)
        {
            Console.Write("\r{0}", formatArgs.Length > 0 ? String.Format(message, formatArgs) : message);
        }

        public void WriteToLogFile(String message, params Object[] formatArgs)
        {
            this.LogStream.Write(formatArgs.Length > 0 ? String.Format(message, formatArgs) : message);
        }

        public void WriteLineToLogFile(String message, params Object[] formatArgs)
        {
            this.LogStream.WriteLine(formatArgs.Length > 0 ? String.Format(message, formatArgs) : message);
        }

        public void WriteToConsoleAndLogFile(String message, params Object[] formatArgs)
        {
            this.WriteToConsole(message, formatArgs);
            this.WriteToLogFile(message, formatArgs);
        }

        public void WriteLineToConsoleAndLogFile(String message, params Object[] formatArgs)
        {
            this.WriteLineToConsole(message, formatArgs);
            this.WriteLineToLogFile(message, formatArgs);
        }

        public void BlankLineInConsole()
        {
            Console.WriteLine();
        }

        public void BlankLineInLogFile()
        {
            this.LogStream.WriteLine();
        }

        public void BlankLineInConsoleAndLogFile()
        {
            this.BlankLineInConsole();
            this.BlankLineInLogFile();
        }

        public void WriteProductInfo(List<Product> productList)
        {
            if (this.options.HasFlag(Shared.Options.Excel))
            {
                this.WriteProductsToCsv(productList);
            }
            if (this.options.HasFlag(Shared.Options.Sql))
            {
                this.WriteProductsToSql(productList);
            }
        }

        private void WriteProductsToCsv(List<Product> productList)
        {
            foreach (Product product in productList.OrderBy(p => p.Category).ThenBy(p => p.Name))
            {
                this.CsvStream.WriteLine("{0}, {1}, {2}{3}{4}, {5}, {6}, {7}",
                    product.OmId, product.VariantId, product.Name.Replace(',', '?'),
                    String.IsNullOrEmpty(product.Size) ? String.Empty : " ", product.Size.Replace(',', '?'), product.OmPrice.ToString("C"),
                    product.Price.ToString("C"), product.IsInStock ? "In stock" : "OUT OF STOCK");
            }
        }

        private void WriteProductsToSql(List<Product> productList)
        {
            // Wipe out existing bulk products without prior basket orders
            this.SqlStream.WriteLine(@"DELETE FROM kvfc_products 
WHERE IFNULL(kvfc_products.bulk_sku, '') != ''
AND kvfc_products.pvid NOT IN (
	SELECT x.pvid
    FROM (
		SELECT kvfc_products.pvid
		FROM kvfc_products 
		JOIN kvfc_basket_items USING (product_id, product_version)
		WHERE IFNULL(bulk_sku, '') != ''
	) AS x
);");

            foreach (Product product in productList.OrderBy(p => p.OmId).ThenBy(p => p.VariantId))
            {
                this.SqlStream.WriteLine("CALL {0}('{1}', '{2}', '{3}', '{4}', {5}, '{6}', '{7}', {8});",
                    ConfigurationManager.AppSettings["SqlProcName"],
                    String.Format("{0}_{1}", product.OmId, product.VariantId), product.Name, product.Description.Replace("'", "''"), product.Category, product.Price,
                    product.Size, this.startDate.ToString("yyyy-MM-dd HH:mm:ss"), product.IsInStock ? "0" : "1");
            }
            // Proc parameters:
            //prm_sku VARCHAR(20),
            //prm_name VARCHAR(75),
            //prm_description LONGTEXT,
            //prm_category VARCHAR(50),
            //prm_unit_price DECIMAL(9, 3),
            //prm_pricing_unit VARCHAR(50),
            //prm_modified DATETIME,
            //prm_is_unlisted BOOLEAN

            // Unlist all products that were not touched by this import
            this.SqlStream.WriteLine("UPDATE kvfc_products SET confirmed = 0, listing_auth_type = 'unlisted' WHERE producer_id IN (SELECT producer_id FROM kvfc_producers WHERE IFNULL(is_bulk, 0) = 1) AND modified < '{0}'",
                startDate.ToString("yyyy-MM-dd HH:mm:ss"));
        }
        
        #endregion Methods

        #region IDisposable members

        private void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    if (this.LogStream != null)
                    {
                        this.LogStream.Close();
                        this.LogStream.Dispose();
                    }
                    if (this.CsvStream != null)
                    {
                        this.CsvStream.Close();
                        this.CsvStream.Dispose();
                    }
                    if (this.SqlStream != null)
                    {
                        this.SqlStream.Close();
                        this.SqlStream.Dispose();
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
