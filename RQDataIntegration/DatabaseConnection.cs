using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.Text.RegularExpressions;

namespace RQDataIntegration
{
    internal class DatabaseConnection
    {
        internal static SqlConnection Connect()
        {
            var dbUserName = ConfigurationManager.AppSettings["ServerUserName"];
            var dbPassword = ConfigurationManager.AppSettings["ServerPassword"];
            var dbName = ConfigurationManager.AppSettings["DatabaseName"];
            var serverName = ConfigurationManager.AppSettings["ServerName"];

            string connetionString = $"Data Source={serverName};Initial Catalog={dbName};User ID={dbUserName};Password={dbPassword}";

            return new SqlConnection(connetionString);
        }

        internal static void ReadDataToDatabase(DataTable data, string tableName)
        {
            var division = HttpRestClient.AuthenticationInfo.Division;

            try
            {
                var connection = Connect();
                var columns = TableColumns(tableName);
                
                foreach (var column in data.Columns)
                {
                    if (!columns.Contains(column.ToString()))
                    {
                        AddColumnToTable(tableName, column.ToString());
                        SmtpHandler.SendMessage($"RQ API import added column {tableName}.", $"Column {column} added to {tableName} because it did not exist in the staging table and came through for {division}.");
                    }
                }
                
                using (var bulkCopy = new SqlBulkCopy(connection))
                {
                    Console.WriteLine($"Attempting to save data to {tableName} SQL table for {division}.");

                    connection.Open();
                    foreach (DataColumn column in data.Columns)
                    {
                        if (column.ColumnName != "Table_Id")
                            bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                    }
                    bulkCopy.BulkCopyTimeout = 600;
                    bulkCopy.DestinationTableName = tableName;
                    bulkCopy.BatchSize = 10000;
                    try
                    {
                        bulkCopy.WriteToServer(data);
                    }
                    catch (SqlException e)
                    {
                        if (e.Message.Contains("Received an invalid column length from the bcp client for colid"))
                        {
                            var colNum = Regex.Match(e.Message, @"\d+").Value;
                            var colName = columns[Convert.ToInt32(colNum) - 1];
                            UpdateColumnLength(tableName, colName);
                            bulkCopy.WriteToServer(data);
                        }
                        else
                        {
                            throw;
                        }
                    }
                    connection.Close();

                    Console.WriteLine($"Successfully saved data to {tableName} SQL table for {division} :D");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unable to save {division} data to {tableName} table.");
                SmtpHandler.SendMessage($"RQ API import error: Unable to save {division} data to {tableName} table", $"Unable to save {division} data to {tableName} table. \n\n{e}\n\n{e.InnerException}");
            }
        }

        internal static void UpdateColumnLength(string tableName, string colName)
        {
            try
            {
                var sql = $"ALTER TABLE {tableName} ALTER COLUMN [{colName}] varchar(5000);";
                var connection = Connect();
                var command = new SqlCommand(sql, connection);

                connection.Open();

                command.ExecuteNonQuery();

                connection.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine($"There was a problem adding {colName} column to {tableName} database table.\n\nERROR: " + e);
                throw;
            }
        }

        internal static void AddColumnToTable(string tableName, string columnName)
        {
            try
            {
                var sql = $"ALTER TABLE {tableName} ADD [{columnName}] varchar(255);";
                var connection = Connect();
                var command = new SqlCommand(sql, connection);

                connection.Open();

                command.ExecuteNonQuery();

                connection.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine($"There was a problem adding {columnName} column to {tableName} database table.\n\nERROR: " + e);
                throw;
            }
        }

        internal static List<string> TableColumns(string tableName)
        {
            try
            {
                var ds = new DataSet();
                var sql = $"SELECT TOP 1 * FROM {tableName} T";
                var connection = Connect();
                var command = new SqlCommand(sql, connection);
                var columns = new List<string>();

                connection.Open();

                var adpter = new SqlDataAdapter(command);
                adpter.Fill(ds);
                var values = ds.Tables[0];

                foreach (var column in values.Columns)
                {
                    columns.Add(column.ToString());
                }

                connection.Close();
                adpter.Dispose();

                return columns;
            }
            catch (Exception e)
            {
                Console.WriteLine("There was a problem geting list of columns from database.\n\nERROR: " + e);
                throw;
            }
        }

        internal static DateTime GetMaxStagingDate(string tableName)
        {
            try
            {
                var ds = new DataSet();
                var sql = $"SELECT MAX(S.DateCreated) FROM staging.{tableName} S";
                var connection = Connect();
                var command = new SqlCommand(sql, connection);

                connection.Open();

                var adpter = new SqlDataAdapter(command);
                adpter.Fill(ds);
                var values = ds.Tables[0];

                connection.Close();
                adpter.Dispose();

                var date = DateTime.Parse(values.Rows[0].ItemArray[0].ToString());

                return date;
            }
            catch (Exception e)
            {
                Console.WriteLine("There was a problem geting the last record creation date from database. " + e);
                throw;
            }
        }
    }
}
