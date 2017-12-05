using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Data;
using Newtonsoft.Json;

namespace RQDataIntegration
{
    internal class DataHandler
    {
        internal static void JsonToCsvFile(DataTable data, string fileName)
        {
            Console.WriteLine($"Preparing {fileName} archive file.");

            string csvOut;

            try
            {
                if (fileName == "")
                {
                    Console.WriteLine($"Cannot save data for {HttpRestClient.AuthenticationInfo.Division} to undetermined archive file.");
                    return;
                }
                
                var sb = new StringBuilder();

                var columnNames = new List<string>();

                foreach (var columnName in data.Columns.Cast<DataColumn>().Select(column => column.ColumnName)) {
                    columnNames.Add(columnName);
                }
                sb.Append("\"");
                sb.AppendLine(string.Join("\",\"", columnNames));

                foreach (DataRow row in data.Rows)
                {
                    IEnumerable<string> fields = row.ItemArray.Select(field => field.ToString().Replace("\"","\"\""));

                    sb.Length--;
                    sb.Length--;
                    sb.Append("\"\n\"");
                    sb.AppendLine(string.Join("\",\"", fields));
                    sb.Length--;
                    sb.Append("\"");
                }
                
                sb.Length--;
                sb.Length--;
                sb.Append("\"");

                if (data.Rows.Count == 0)
                {
                    SmtpHandler.SendMessage("RQ API import error: Unknown XML element",
                        $"Unknown XML element. Unable to parse {HttpRestClient.AuthenticationInfo.Division} {fileName} data for saving to file.");
                    return;
                }

                if (sb.Length == 0)
                {
                    SmtpHandler.SendMessage("RQ API import error: There were no records in xml data",
                        $"There were no records in {HttpRestClient.AuthenticationInfo.Division} {fileName} xml data to save to csv. Please check that xml is being generated properly.");
                    return;
                }

                csvOut = sb.ToString();
            }
            catch (Exception e)
            {
                Console.WriteLine($"A problem was encountered when preparing {fileName} archive file. \n\n" + e);
                SmtpHandler.SendMessage($"RQ API import error: Unable to prepare {HttpRestClient.AuthenticationInfo.Division} {fileName} data for archive file", $"Unable to prepare {HttpRestClient.AuthenticationInfo.Division} {fileName} report data for archive CSV. \n\n{e}\n\n{e.InnerException}");
                return;
            }

            SaveDataToCsvFile(csvOut, fileName);
        }

        internal static void XmlToCsvFile(XmlDocument doc, string fileName, int division, DateTime date)
        {
            Console.WriteLine($"Preparing {fileName} archive file.");

            string csvOut;

            try
            {
                if (fileName == "")
                {
                    Console.WriteLine($"Cannot save data for {division} to undetermined archive file.");
                    return;
                }

                var xml = XDocument.Parse(doc.InnerXml);

                var sb = new StringBuilder();
                var record = new Dictionary<string, string>();
                // Add record level node names here if there is a change.
                var nodes = new List<string>() { "Record", "Table1", "Records" };
                var nodeName = "";

                foreach (var node in nodes)
                {
                    if (xml.Descendants(node).Count() <= 1) continue;

                    nodeName = node;

                    foreach (var element in xml.Descendants(nodeName).Elements())
                    {
                        if (!record.ContainsKey(element.Name.ToString()))
                        {
                            record.Add(element.Name.ToString(), "");
                        }
                    }

                    record.Add("DivisionID", "");
                    record.Add("AccessedDate", "");
                }

                if (record.Count == 0)
                {
                    SmtpHandler.SendMessage("RQ API import error: Unknown XML element",
                        $"Unknown XML element. Unable to parse {HttpRestClient.AuthenticationInfo.Division} {fileName} data for saving to file.");
                    return;
                }

                foreach (var key in record.Keys)
                {
                    sb.Append($"{key},");
                }

                sb.Remove(sb.Length - 1, 1);
                sb.AppendLine();

                foreach (var node in xml.Descendants(nodeName))
                {
                    foreach (var key in record.Keys.ToList())
                    {
                        record[key] = "";
                    }

                    foreach (var element in node.Elements())
                    {
                        record[element.Name.ToString()] = element.Value;
                    }

                    record["DivisionID"] = division.ToString();
                    record["AccessedDate"] = date.ToString(CultureInfo.InvariantCulture);

                    foreach (var value in record.Values)
                    {
                        sb.Append($"\"{value}\",");
                    }

                    sb.Remove(sb.Length - 1, 1);
                    sb.AppendLine();
                }


                if (sb.Length == 0)
                {
                    SmtpHandler.SendMessage("RQ API import error: There were no records in xml data",
                        $"There were no records in {HttpRestClient.AuthenticationInfo.Division} {fileName} xml data to save to csv. Please check that xml is being generated properly.");
                    return;
                }

                csvOut = sb.ToString();
            }
            catch (Exception e)
            {
                Console.WriteLine($"A problem was encountered when preparing {fileName} archive file. \n\n" + e);
                SmtpHandler.SendMessage($"RQ API import error: Unable to prepare {HttpRestClient.AuthenticationInfo.Division} {fileName} data for archive file", $"Unable to prepare {HttpRestClient.AuthenticationInfo.Division} {fileName} report data for archive CSV. \n\n{e}\n\n{e.InnerException}");
                return;
            }

            SaveDataToCsvFile(csvOut, fileName);
        }

        internal static void SaveDataToCsvFile(string response, string fileName)
        {
            Console.WriteLine($"Attempting to save {fileName} report data to archive file.");

            var division = "";
            var date = "";
            var getDate = DateTime.Now.ToString("yyyyMMdd-HHmmss.fff");

            var directoryName = fileName.Contains("Types") ? "Types" : fileName;

            if (directoryName != "Types")
            {
                division = $"{HttpRestClient.AuthenticationInfo.Division}_";
            }

            date = $"_{getDate}";

            var filePathName = $"\\\\tb-sql-reports\\Archive\\Archive\\{directoryName}";
            var fullFileName = $"api_{division}{fileName}{date}.csv";
            var fullPath = Path.GetFullPath($"{filePathName}\\{fullFileName}");

            try
            {
                File.AppendAllText(fullPath, response);
                Console.WriteLine($"Successfully saved {fileName} report data to archive file.");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unable to save {fileName} report data to CSV. \n\n" + e);
                SmtpHandler.SendMessage($"RQ API import error: Unable to save {HttpRestClient.AuthenticationInfo.Division} {fileName} data to archive file" , $"Unable to save {HttpRestClient.AuthenticationInfo.Division} {fileName} report data to archive CSV. \n\n{e}\n\n{e.InnerException}");
                return;
            }

            Console.WriteLine($"{fileName} report data archival complete.");
        }

        internal static DataTable JsonToDataTable(string json, string startDate)
        {
            var dateNotExists = false;
            var data = new DataTable();


            dynamic jdata = JsonConvert.DeserializeObject(json);

            var firstCol = jdata[0];

            foreach (var item in firstCol)
            {
                var name = item.Name;
                data.Columns.Add(name);
            }

            foreach (var dataRow in jdata)
            {
                var row = data.NewRow();
                foreach (var item in dataRow)
                {
                    DateTime tempDate;
                    var dateString = "";

                    if (DateTime.TryParse(item.Value.ToString(), out tempDate))
                    {
                        dateString = item.Value.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    }

                    var n = item.Name;
                    row[n] = tempDate != Convert.ToDateTime("1/1/0001 12:00:00 AM") ? dateString : item.Value;
                }
                data.Rows.Add(row);
            }

            data.Columns.Add("DivisionID");
            data.Columns.Add("AccessedDate");

            if (!data.Columns.Contains("DateCreated"))
            {
                data.Columns.Add("DateCreated");
                dateNotExists = true;
            }

            for (var i = 0; i < data.Rows.Count; i++)
            {
                data.Rows[i]["DivisionID"] = HttpRestClient.AuthenticationInfo.DivisionId;
                // todo: seems to be generating this multuple times per pull...
                data.Rows[i]["AccessedDate"] = DateTime.Now.ToString(CultureInfo.CurrentCulture);
                if (dateNotExists)
                    data.Rows[i]["DateCreated"] = startDate == "" ? DateTime.Now.AddDays(-1) : Convert.ToDateTime(startDate); ;
            }

            return data;
        }
    }
}
