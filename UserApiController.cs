using EskaCMS.Core.BusinessModels;
using EskaCMS.Core.Entities;
using EskaCMS.Core.Enums;
using EskaCMS.Core.Extensions;
using EskaCMS.Core.Models;
using EskaCMS.Core.Services;
using EskaCMS.CoreShared.Services;
using EskaCMS.CoreShared.ViewModels;
using EskaCMS.EskaCoreIntegration.Services.Interfaces;
using EskaCMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static EskaCMS.Core.Enums.GeneralEnums;

namespace EskaCMS.CoreShared.Controllers
{
    [Area("Core")]
    [Route("api/users")]
    [Authorize]
    public class UserApiController : Controller
    {
        private readonly IRepository<User> _userRepository;
        private readonly UserManager<User> _userManager;
        private readonly IRepository<UsersSites> _userSitesRepository;
        private readonly IEskaCoreIntegrationService _eskaCoreIntegrationService;
        private readonly IUserApiService _userApiService;
        private readonly ICurrencyRatesApiService _CurrencyRatesApiService;
        private readonly IWorkContext _workContext;
        private readonly IRepository<EskaCMS.Core.Entities.UsersAndRolesManagement.UserOptions> _UserOptionsRepository;
        public UserApiController(IRepository<User> userRepository, UserManager<User> userManager,
            IRepository<UsersSites> userSitesRepository,
            IEskaCoreIntegrationService eskaCoreIntegrationService,
            IRepository<EskaCMS.Core.Entities.UsersAndRolesManagement.UserOptions> UserOptionsRepository,
            IUserApiService userApiService,
            ICurrencyRatesApiService CurrencyRatesApiService,
            IWorkContext workContext)
        {
            _userRepository = userRepository;
            _userManager = userManager;
            _userSitesRepository = userSitesRepository;
            _UserOptionsRepository = UserOptionsRepository;
            _eskaCoreIntegrationService = eskaCoreIntegrationService;
            _userApiService = userApiService;
            _CurrencyRatesApiService = CurrencyRatesApiService;
            _workContext = workContext;

        }

        [HttpPost]
        [Route("QuickSearch")]
        public async Task<IActionResult> QuickSearch([FromBody] UserSearchOption searchOption)
        {
            try
            {
                long siteId = await _workContext.GetCurrentSiteIdAsync();

                return Ok(await _userApiService.QuickSearch(searchOption, siteId));
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }



        [HttpGet]
        [Route("SyncADUsers")]
        public async Task<IActionResult> SyncADUsers()
        {
            try
            {
                bool DidSyncExc = await _userApiService.SyncADUsers();

                if (DidSyncExc)
                    return Ok(DateTime.Now.ToString("dd-MM-yyyy"));

                return BadRequest("Internal server Error");
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }
        [HttpGet]
        [Route("GetUserTypes")]
        public IActionResult GetUserTypes()
        {
            var enumVals = new List<object>();

            foreach (var item in Enum.GetValues(typeof(EUsersTypes)))
            {

                enumVals.Add(new
                {
                    id = (int)item,
                    name = item.ToString()
                });
            }

            return Ok(enumVals);
        }


        [HttpGet]
        [Route("GetUser/{id}")]
        public async Task<IActionResult> GetUser(long id)
        {
            try
            {
                return Ok(await _userApiService.GetById(id));
            }
            catch (Exception exc)
            {
                return BadRequest(exc.Message);
            }
        }





        [Route("AddUser")]
        [HttpPost, DisableRequestSizeLimit]
        public async Task<IActionResult> AddUser()
        {
            try
            {
                UserForm model = JsonSerializer.Deserialize<UserForm>(Request.Form["data"].ToString());
                long siteId = long.Parse(Request.Headers["siteId"].ToString());

                return Ok(await _userApiService.Create(model, siteId));
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }



        [Route("EditUser/{id}")]
        [HttpPut, DisableRequestSizeLimit]
        public IActionResult EditUser(long id)
        {
            try
            {
                UserForm model = JsonSerializer.Deserialize<UserForm>(Request.Form["data"].ToString());
                long SiteId = long.Parse(Request.Headers["siteId"].ToString());
                IFormFileCollection files = Request.Form.Files;
                return Ok(_userApiService.Update(model, SiteId, id, files));
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }

        }


        [Route("EditUserCurrency")]
        [HttpPut, DisableRequestSizeLimit]
        public async Task<IActionResult> EditUserCurrency([FromBody] EditUserCurrencyVM model)
        {
            try
            {

                var result = await _userApiService.EditUserCurrencyAndCulture(model);

                if (result)
                {

                    return Accepted();
                }


                return BadRequest();
            }
            catch (Exception E)
            {
                return BadRequest();
            }
        }


        [Route("EditUserOption")]
        [HttpPut]
        public async Task<IActionResult> EditUserOption([FromBody] UserOptionsVM userOptionsVM)
        {
            try
            {
                await _userApiService.EditUserOption(userOptionsVM);
                return Ok("edit successfully"
                    );
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }


        [HttpDelete]
        [Route("deleteUser/{id}")]
        public async Task<IActionResult> DeleteUser(long id)
        {
            try
            {
                await _userApiService.Delete(id);
                return Ok();
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }
        [HttpDelete]
        [Route("deleteUserOption/{id}")]
        public async Task<IActionResult> DeleteUserOption(long id)
        {
            try
            {
                await _userApiService.DeleteUserOption(id);
                return Ok("Deleted Successfuly");
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpPost]
        [Route("GetList")]
        public async Task<IActionResult> GetList([FromBody] UserSearchTableParam<UserListSearchVM> param,GeneralEnums.FillterType? fillterType)
        {
            try
            {

                return Ok(await _userApiService.GetList(param,fillterType));
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }
        [HttpGet]
        [Route("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            try
            {

                return Ok(await _userApiService.GetAll());
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet]
        [Route("GetUserDetails/{UserId}")]
        public async Task<IActionResult> GetUserDetails(long UserId)
        {
            try
            {
                return Ok(await _userApiService.GetById(UserId));
            }
            catch (Exception exc)
            {
                return BadRequest(exc.Message);
            }
        }
       
    }
}
