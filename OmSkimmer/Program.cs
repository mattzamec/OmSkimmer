using System;

namespace OmSkimmer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Write("Generate (D)ry run (nothing written, only parsed for testing), (X)cel files only, (S)QL only, or (B)oth? Default is Both. ");
            String option = Console.ReadKey().KeyChar.ToString().ToUpper();
            Console.WriteLine();

            Shared.Options options;
            switch (option)
            {
                case "D":
                    options = Shared.Options.None;
                    break;
                case "X":
                    options = Shared.Options.Excel;
                    break;
                case "S":
                    options = Shared.Options.Sql;
                    break;
                default:
                    options = Shared.Options.Excel | Shared.Options.Sql;
                    break;
            }

            Console.Write("Enter a number of products to process (for test run) or just hit <Enter> to process all ");
            String processNumber = Console.ReadLine();
            Int32 numberToProcess;
            if (String.IsNullOrEmpty(processNumber) || !Int32.TryParse(processNumber, out numberToProcess))
            {
                numberToProcess = 0;
            }

            using (HtmlParser parser = new HtmlParser(options, numberToProcess))
            {
                parser.ParseOmData();
            }

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("<Enter> to exit.");
            Console.ReadLine();
        }
    }
}
