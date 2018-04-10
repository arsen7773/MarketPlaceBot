﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyTelegramBot.BusinessLayer;
using Telegram.Bot.Types;
using Telegram.Bot;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Data.SqlClient;

namespace MyTelegramBot.Controllers
{
    [Produces("application/json")]

    public class HostingVersionController : Controller
    {

        TelegramBotClient BotClient { get; set; }

        MarketBotDbContext DbContext { get; set; }

        private string Result { get; set; }

        private Model.HostInfo HostInfo { get; set; }

        [HttpGet]
        public IActionResult Install(string Token, string BotName, string UrlWebHook, bool IsDemo=false)
        {
            string dbname = BotName + "Db";

            HostInfo = new Model.HostInfo();

            if (CreateDb(dbname) && InsertNewBotToDb(Token,BotName, UrlWebHook, true))
            {
                string read = ReadFile("HostInfo.json");

                if (read != null)
                    HostInfo = JsonConvert.DeserializeObject<Model.HostInfo>(read);

                HostInfo.CreateTimeStamp = DateTime.Now;
                HostInfo.BotName = BotName;
                HostInfo.IsDemo = IsDemo;
                HostInfo.IsFree = false;
                HostInfo.Token = Token;
                HostInfo.UrlWebHook = UrlWebHook;
                HostInfo.DbConnectionString = String.Format("Server=localhost;Database={0};Integrated Security = FALSE;Trusted_Connection = True;", dbname);
                HostInfo.DbName = dbname;

                WriteFile("connection.json", HostInfo.DbConnectionString);

                WriteFile("HostInfo.json", JsonConvert.SerializeObject(HostInfo));

                Dictionary<string, string> dictionary = new Dictionary<string, string>
                {
                    { "name", BotName }
                };

                WriteFile("name.json", JsonConvert.SerializeObject(dictionary));

                return Json(HostInfo);

            }

            else
            {
                Dictionary<string, string> dictionary = new Dictionary<string, string>
                {
                    { "Ok", "false" },
                    { "Result", Result }
                };

                return Json(dictionary);
            }
        }

        [HttpGet]
        public IActionResult Unistall()
        {
            try
            {
                string read = ReadFile("HostInfo.json");

                Model.HostInfo hostInfo = JsonConvert.DeserializeObject<Model.HostInfo>(read);

                DetachDatabase(hostInfo.DbName);

                DeleteDatabaseFile(hostInfo.DbName);

                hostInfo.BotName = null;
                hostInfo.DbConnectionString = null;
                hostInfo.Token = null;
                hostInfo.IsFree = true;
                hostInfo.DbName = null;
                hostInfo.UrlWebHook = null;
                hostInfo.Blocked = false;

                WriteFile("HostInfo.json", JsonConvert.SerializeObject(hostInfo));

                return Ok();
            }

            catch
            {
                return NotFound();
            }
        }

        [HttpGet]
        public IActionResult Block()
        {
            BusinessLayer.ConfigurationFunction.BotBlocked();

            return Ok();
        }

        [HttpGet]
        public IActionResult UnBlock()
        {
            BusinessLayer.ConfigurationFunction.BotUnblocked();

            return Ok();
        }

        [HttpGet]
        /// <summary>
        /// отправить владельцу бота с помощью его же бота текстовое сообщение
        /// </summary>
        /// <param name="Text"></param>
        /// <returns></returns>
        public async Task<IActionResult> SendTextMsgToOwner(string Text)
        {
            var  result = await Bot.GeneralFunction.SendTextMsgToOwner(Text);

            if (result != null)
                return Ok();

            else
               return NotFound();
        }

        [HttpGet]
        public IActionResult Test()
        {
            return Ok();
        }

        [HttpGet]
        public IActionResult GetInfo()
        {
            Model.HostInfo hostInfo = JsonConvert.DeserializeObject<Model.HostInfo>(ReadFile("HostInfo.json"));

            return Json(hostInfo);
        }

        private string ReadFile(string Path)
        {
            try
            {
                StreamReader reader = new StreamReader(Path);

                string Result = reader.ReadToEnd();

                reader.Dispose();

                return Result;
            }

            catch
            {
                return null;
            }
        }

        private bool WriteFile(string Path, string Text)
        {
            try
            {
                StreamWriter streamWriter = new StreamWriter(Path);

                streamWriter.Write(Text);

                streamWriter.Flush();

                streamWriter.Dispose();

                return true;
            }

            catch
            {
                return false;
            }
        }

        private bool CreateDb(string DbName)
        {
            SqlConnection sqlConnection = null;

            try
            {

                sqlConnection = new SqlConnection("Server=localhost;Database=master;Integrated Security = FALSE;Trusted_Connection = True;");
                sqlConnection.Open();

                string CreateSqlQuery = String.Format("CREATE DATABASE [{0}]  CONTAINMENT = NONE " +
                " ON  PRIMARY (NAME =N'{0}', FILENAME = N'" + @"C:\Program Files\Microsoft SQL Server\MSSQL12.MSSQLSERVER\MSSQL\DATA\{0}.mdf' , SIZE = 4288KB , MAXSIZE = UNLIMITED, FILEGROWTH = 1024KB )," +
                "  FILEGROUP [BotDbFs] CONTAINS FILESTREAM  DEFAULT" +
                " ( NAME = N'{0}_fs', FILENAME = N'" + @"C:\Program Files\Microsoft SQL Server\MSSQL12.MSSQLSERVER\MSSQL\DATA\{0}_fs' , MAXSIZE = UNLIMITED)  LOG ON " +
                " ( NAME = N'{0}_log', FILENAME = N'" + @"C:\Program Files\Microsoft SQL Server\MSSQL12.MSSQLSERVER\MSSQL\DATA\{0}_log.ldf' , SIZE = 1072KB , MAXSIZE = 2048GB , FILEGROWTH = 10%)", DbName);

                SqlCommand sqlCommand = new SqlCommand(CreateSqlQuery, sqlConnection);
                sqlCommand.ExecuteNonQuery();

                sqlCommand = new SqlCommand("USE " + DbName + " " + ReadFile("SQL\\alter.sql"), sqlConnection);
                sqlCommand.ExecuteNonQuery();

                sqlCommand = new SqlCommand("USE " + DbName + " " + ReadFile("SQL\\insert.txt"), sqlConnection);
                sqlCommand.ExecuteNonQuery();
                
                Result= "Успешно созада база данных " + DbName;

                return true;


            }

            catch (Exception e)
            {
                Result = e.Message;
                return false;
            }

            finally
            {
                sqlConnection.Close();
            }
        }

        private bool InsertNewBotToDb(string token, string name, string Url, bool IsServerVersion = false)
        {
            try
            {

                DbContext = new MarketBotDbContext();

                if (token != null && name != null && Url != null)
                {
                    var spl = token.Split(':');
                    int chat_id = Convert.ToInt32(spl[0]);

                    BotInfo botInfo = new BotInfo
                    {
                        Token = token,
                        Name = name,
                        WebHookUrl = Url,
                        Timestamp = DateTime.Now,
                        HomeVersion = !IsServerVersion,
                        ServerVersion = IsServerVersion,
                        ChatId = chat_id

                    };

                    DbContext.BotInfo.Add(botInfo);
                    DbContext.SaveChanges();

                    var conf = new Configuration { BotInfoId = botInfo.Id, VerifyTelephone = false, OwnerPrivateNotify = false, Delivery = true, Pickup = false, ShipPrice = 0, FreeShipPrice = 0, CurrencyId = 1, BotBlocked = false };
                    DbContext.Configuration.Add(conf);
                    DbContext.SaveChanges();

                    Company company = new Company { Instagram = "https://www.instagram.com/", Vk = "https://www.vk.com/", Chanel = "https://t.me/", Chat = "https://t.me/" };
                    DbContext.Company.Add(company);
                    DbContext.SaveChanges();

                    return true;
                }

                else
                    return false;
            }

            catch (Exception e)
            {
                Result = e.Message;
                return false;
            }

            finally
            {
                DbContext.Dispose();
            }
        }

        /// <summary>
        /// Отсоеденить базу данных
        /// </summary>
        /// <param name="DbName"></param>
        /// <returns></returns>
        private bool DetachDatabase(string DbName)
        {
            SqlConnection sqlConnection = null;

            try
            {
                sqlConnection = new SqlConnection("Server=localhost;Database=master;Integrated Security = FALSE;Trusted_Connection = True;");
                sqlConnection.Open();

                SqlCommand sqlCommand = new SqlCommand(String.Format("EXEC sp_detach_db '{0}', 'true';",DbName), sqlConnection);
                sqlCommand.ExecuteNonQuery();

                return true;
            }

            catch
            {
                return false;
            }

            finally
            {
                sqlConnection.Close();
            }
        }

        private bool DeleteDatabaseFile(string DbName)
        {
            string path = @"C:\Program Files\Microsoft SQL Server\MSSQL12.MSSQLSERVER\MSSQL\DATA\";

            try
            {
                System.IO.File.Delete(String.Format(path + "{0}.mdf", DbName));

                System.IO.File.Delete(String.Format(path + "{0}_log.ldf", DbName));

                Directory.Delete(String.Format(path+ "{0}_fs",DbName), true);

                return true;
            }

            catch
            {
                return false;
            }
        }
    }
}