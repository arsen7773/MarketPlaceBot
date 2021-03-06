﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ManagementBots.Db;

namespace ManagementBots.Controllers
{
    [Produces("application/json")]

    public class UserController : Controller
    {
        BotMngmntDbContext DbContext { get; set; }

        public IActionResult Index()
        {
            try
            {
                DbContext = new BotMngmntDbContext();

                return View(DbContext.Follower.ToList());
            }

            catch (Exception e)
            {
                return Json(e.Message);
            }

            finally
            {
                DbContext.Dispose();
            }

        }
    }
}