
using EskaCms.CoreShared.Helpers;
using EskaCMS.Core.BusinessModels;
using EskaCMS.Core.Entities;
using EskaCMS.Core.Enums;
using EskaCMS.Core.Extensions;
using EskaCMS.CoreShared.ViewModels;
using EskaCMS.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using static EskaCMS.Core.Entities.BusinessModels.BusinessModel;
using static EskaCMS.Core.Enums.GeneralEnums;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using EskaCMS.Core.Areas.Core.ViewModels.Account;
using EskaCMS.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using EskaCMS.Core.Models;
using EskaCms.CoreShared.ViewModels;
using EskaCMS.EskaCoreIntegration.Services.Interfaces;
using EskaCMS.EskaCoreIntegration.ViewModels;
using EskaCMS.ADIntegration.ViewModels;
using EskaCMS.Security.Entities;

namespace EskaCMS.CoreShared.Services
{
    public class UserApiService : IUserApiService
    {
        private readonly IEskaCoreIntegrationService _eskaCoreIntegrationService;
        private FileHelper fileHelper;
        private readonly IRepository<User> _userRepository;
        private readonly IRepository<CustomerGroup> _groupRepository;
        private readonly UserManager<User> _userManager;
        private readonly IRepository<UsersSites> _userSitesRepository;
        private readonly IRepository<EskaCMS.Core.Entities.UsersAndRolesManagement.UserOptions> _userOptionsRepository;
        private readonly ILogger<IRepository<User>> _UserRepoLogger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IWorkContext _workContext;
        private readonly IRepository<UserLoginTransactions> _UserLoginTransactions;

        private static bool SyncingUsersStarted = false;
        private readonly RoleManager<Role> _roleManager;
        public UserApiService(
            IEskaCoreIntegrationService eskaCoreIntegrationService,
            IWebHostEnvironment webHostEnvironmen,
            IRepository<User> userRepository,
            UserManager<User> userManager,
            IRepository<UsersSites> userSitesRepository,
            IRepository<CustomerGroup> groupRepository,
            IRepository<EskaCMS.Core.Entities.UsersAndRolesManagement.UserOptions> userOptionsRepository,
            IRepository<UserLoginTransactions> UserLoginTransactions,
            ILogger<IRepository<User>> logger,
            IServiceScopeFactory serviceScopeFactory,
              RoleManager<Role> roleManager,
        IWorkContext workContext
            )
        {
            _eskaCoreIntegrationService = eskaCoreIntegrationService;
            fileHelper = new FileHelper(webHostEnvironmen);
            _userRepository = userRepository;
            _userManager = userManager;
            _userSitesRepository = userSitesRepository;
            _userOptionsRepository = userOptionsRepository;
            _groupRepository = groupRepository;
            _UserRepoLogger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _workContext = workContext;
            _UserLoginTransactions = UserLoginTransactions;
            _roleManager = roleManager;
        }
        public async Task<long> Create(UserForm model, long siteId)
        {
            if (_userManager.Users.Any(x => x.PhoneNumber == model.PhoneNumber))
            {
                throw new ValidationException("Phone Number Already Exists");
            }
            if (_userManager.Users.Any(x => x.UserName == model.UserName))
            {
                throw new ValidationException("UserName Already Exists");
            }
            User user = new User
            {
                UserName = model.UserName,
                Email = model.Email,
                FullName = model.FullName,
                PhoneNumber = model.PhoneNumber,
                VendorId = model.VendorId,
               // Type = model.UserType,
                CreatedById = model.CreatedById,
                IsADAuthRequired = model.IsADAuthRequired,
                IsCoreIntegration = model.IsCoreIntegration,
                ThumbnailId = model.ThumbnailId,
              //  Status=model.Status
            };

            if (model.UserImage != null)
            {
                user.Image = fileHelper.PostFile(model.UserImage, siteId);
            }
            if (model.RoleIds != null)
            {
                user.UserRoles = AddUserRoles(model.RoleIds);
            }

            if (model.CustomerGroupIds != null)
            {
                user.CustomerGroups = AddUserGroups(model.CustomerGroupIds);
            }
            IdentityResult result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                if (user.IsCoreIntegration)
                {
                   EskaCoreBaseResponseVM<List<CoreGroupVM>> userInfo = await _eskaCoreIntegrationService.GetUserInfo("", user.UserName);
                    if (userInfo.content.Count == 0)
                    {
                        await _eskaCoreIntegrationService.InsertUser(model);
                    }
                }
                UsersSites UserToSite = new UsersSites { UserId = user.Id, SiteId = Convert.ToInt64(siteId), IsDefault = true };
                _userSitesRepository.Add(UserToSite);
                _userSitesRepository.SaveChanges();
                AddUserOptions(model.UserOptions, user.Id);

                return user.Id;
            }
            else
            {
                throw new ValidationException("User creation failed");
            }

        }

        private static List<UserRole> AddUserRoles(IList<long> rolesIds)
        {
            List<UserRole> userRoles = new List<UserRole>();
            foreach (var roleId in rolesIds)
            {
                UserRole userRole = new UserRole
                {
                    RoleId = roleId
                };

                userRoles.Add(userRole);
            }
            return userRoles;
        }
        private static List<CustomerGroupUser> AddUserGroups(IList<long> groupIds)
        {
            List<CustomerGroupUser> userGroups = new List<CustomerGroupUser>();
            foreach (var customergroupId in groupIds)
            {
                var userCustomerGroup = new CustomerGroupUser
                {
                    CustomerGroupId = customergroupId
                };

                userGroups.Add(userCustomerGroup);
            }
            return userGroups;
        }
        private void AddUserOptions(List<UserOptionsVM> userOptions, long userId)
        {
            if (userOptions != null)
            {
                foreach (var item in userOptions)
                {
                    EskaCMS.Core.Entities.UsersAndRolesManagement.UserOptions objUserOpt = new EskaCMS.Core.Entities.UsersAndRolesManagement.UserOptions();

                    objUserOpt.PropertyName = item.PropertyName;
                    objUserOpt.Value = item.Value;
                    objUserOpt.ControlType = item.ControlType;
                    objUserOpt.IsEditable = item.IsEditable;
                    objUserOpt.IsVisible = item.IsVisible;
                    objUserOpt.UserId = userId;
                    _userOptionsRepository.Add(objUserOpt);
                    _userOptionsRepository.SaveChanges();
                }
            }
        }
        public async Task<long> Update(UserForm model, long siteId, long userId, IFormFileCollection files)
        {
            User user = await _userRepository.Query().Include(x => x.UserRoles).Include(x => x.CustomerGroups).FirstOrDefaultAsync(x => x.Id == userId);
            user.Email = model.Email;
            user.UserName = model.UserName;
            user.FullName = model.FullName;
            user.PhoneNumber = model.PhoneNumber;
            user.VendorId = model.VendorId;
            user.Type = model.UserType;
            user.Status = model.Status;
            user.IsADAuthRequired = model.IsADAuthRequired;
            user.IsCoreIntegration = model.IsCoreIntegration;
            user.ThumbnailId = model.ThumbnailId;
            AddOrDeleteRoles(model, user);
            AddOrDeleteCustomerGroups(model, user);
            if (model.UserOptions != null)
            {
                foreach (var item in model.UserOptions)
                {
                    EskaCMS.Core.Entities.UsersAndRolesManagement.UserOptions objUserOpt = new EskaCMS.Core.Entities.UsersAndRolesManagement.UserOptions();

                    objUserOpt.PropertyName = item.PropertyName;
                    objUserOpt.Value = item.Value;
                    objUserOpt.ControlType = item.ControlType;
                    objUserOpt.IsEditable = item.IsEditable;
                    objUserOpt.IsVisible = item.IsVisible;
                    objUserOpt.UserId = user.Id;
                    _userOptionsRepository.Add(objUserOpt);
                    _userOptionsRepository.SaveChanges();
                }
            }
            if (files.Count > 0)
            {
                user.Image = fileHelper.UpdateFile(files[0], user.Image, siteId);
            }
            IdentityResult result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {

                List<UsersSites> UserToSite = _userSitesRepository.Query().Where(x => x.UserId == userId).ToList();
                var UserSiteIds = UserToSite.Select(x => x.Id).ToList();
                var LoginTransactions = await _UserLoginTransactions.Query().Where(u => UserSiteIds.Contains(u.UserSiteId)).ToListAsync();

                _UserLoginTransactions.RemoveRange(LoginTransactions);

                await _UserLoginTransactions.SaveChangesAsync();

                _userSitesRepository.RemoveRange(UserToSite);

                //List<UserLoginTransactions> users = await _UserLoginTransactions.Query().Where(x => UserToSite.Contains(x.UserSite)).ToListAsync();


                //foreach (var item in UserToSite)
                //{
                //    _userSitesRepository.Remove(item);
                //}

                if (user.IsCoreIntegration && model.CoreGroupsIds != null)
                {
                    foreach (var group in model.CoreGroupsIds)
                    {
                        InsertCoreGroupVM insertCoreGroupVM = new InsertCoreGroupVM
                        {
                            CRG_COM_ID = 1,
                            CSR_GRP_ID = group,
                            USERNAME = user.UserName
                        };
                        await _eskaCoreIntegrationService.InsertUserGroup(insertCoreGroupVM);
                    }
                }
                if (user.IsCoreIntegration)
                {
                   EskaCoreBaseResponseVM<List<CoreGroupVM>> userInfo = await _eskaCoreIntegrationService.GetUserInfo("", user.UserName);
                    if (userInfo.content.Count == 0)
                    {
                        await _eskaCoreIntegrationService.InsertUser(model);
                    }
                }

                for (int i = 0; i < model.AssignedSites.Count; i++)
                {
                    bool IsDefault = false;

                    if (i == 0)
                        IsDefault = true;
                    else
                        IsDefault = false;

                    var AddUserToSite = new UsersSites { UserId = user.Id, SiteId = model.AssignedSites[i], IsDefault = IsDefault };
                    _userSitesRepository.Add(AddUserToSite);
                }
              
                _userSitesRepository.SaveChanges();


                return user.Id;
            }
            else
            {
                throw new ValidationException("Something went wrong");
            }

        }
        private void AddOrDeleteRoles(UserForm model, User user)
        {
            foreach (var roleId in model.RoleIds)
            {
                if (user.UserRoles.Any(x => x.RoleId == roleId))
                {
                    continue;
                }

                var userRole = new UserRole
                {
                    RoleId = roleId,
                    User = user
                };
                user.UserRoles.Add(userRole);
            }

            var deletedUserRoles =
                user.UserRoles.Where(userRole => !model.RoleIds.Contains(userRole.RoleId))
                    .ToList();

            foreach (var deletedUserRole in deletedUserRoles)
            {
                deletedUserRole.User = null;
                user.UserRoles.Remove(deletedUserRole);
            }
        }
        private void AddOrDeleteCustomerGroups(UserForm model, User user)
        {
            if (model.CustomerGroupIds != null)
            {
                foreach (var customergroupId in model.CustomerGroupIds)
                {
                    if (user.CustomerGroups.Any(x => x.CustomerGroupId == customergroupId))
                    {
                        continue;
                    }
                   
                    var userCustomerGroup = new CustomerGroupUser
                    {
                        CustomerGroupId = customergroupId,
                        User = user
                    };
                    user.CustomerGroups.Add(userCustomerGroup);
                }
            }


            var deletedUserCustomerGroups = user.CustomerGroups.ToList();
            if (model.CustomerGroupIds != null)
            {
                deletedUserCustomerGroups = user.CustomerGroups.Where(userCustomerGroup => !model.CustomerGroupIds.Contains(userCustomerGroup.CustomerGroupId))
            .ToList();
            }

            foreach (var deletedUserCustomerGroup in deletedUserCustomerGroups)
            {
                deletedUserCustomerGroup.User = null;
                user.CustomerGroups.Remove(deletedUserCustomerGroup);
            }
        }
        public async Task EditUserOption(UserOptionsVM userOptionsVM)
        {
            var userOptions = await _userOptionsRepository.Query().FirstOrDefaultAsync(x => x.Id == userOptionsVM.Id);
            userOptions.ControlType = userOptionsVM.ControlType;
            userOptions.IsEditable = userOptionsVM.IsEditable;
            userOptions.IsVisible = userOptionsVM.IsVisible;
            userOptions.PropertyName = userOptionsVM.PropertyName;
            userOptions.Value = userOptionsVM.Value;
            await _userOptionsRepository.SaveChangesAsync();
        }

        public async Task<bool> EditUserCurrencyAndCulture(EditUserCurrencyVM Model)
        {
            var UserId = await _workContext.GetCurrentUserId();
            var SiteId = await _workContext.GetCurrentSiteIdAsync();

            var UserSiteObj = _userSitesRepository
                                .Query()
                                .Where(u => u.SiteId == SiteId && u.UserId == UserId)
                                .FirstOrDefault();

            if (UserSiteObj == null || UserId == 0)
            {
                return false;
            }

            UserSiteObj.CurrencyId = Model.CurrencyId;
            UserSiteObj.CultureId = Model.CultureId;

            await _userSitesRepository.SaveChangesAsync();

           return true;            

        }

        




        public async Task DeleteUserOption(long id)
        {
            var userOption = await _userOptionsRepository.Query().FirstOrDefaultAsync(x => x.Id == id);
            if (userOption == null)
            {
                throw new ValidationException("User option not found");
            }

            _userOptionsRepository.Remove(userOption);
            await _userOptionsRepository.SaveChangesAsync();
        }
        public async Task Delete(long id)
        {
            User user = await _userRepository.Query().FirstOrDefaultAsync(x => x.Id == id);
            if (user == null)
            {
                throw new ValidationException("User not found");
            }

            user.UserName = user.Id + "Deleted" + user.UserName;
            user.NormalizedUserName = user.Id + "Deleted" + user.NormalizedUserName;

            user.Status = EStatus.Deleted;
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.Now.AddYears(200);
            await _userRepository.SaveChangesAsync();
        }

        public async Task<UserForm> GetById(long id)
        {

            UserForm user = await _userManager.Users
                .Include(x => x.UserRoles)
                .Include(x => x.CustomerGroups)
                .Include(x => x.UserOptions)
                .Where(x => x.Id == id).Select(user => new UserForm
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    UserName = user.UserName,
                    PhoneNumber = user.PhoneNumber,
                    VendorId = user.VendorId,
                    UserType = user.Type,
                    Status = user.Status,
                    Image = user.Image,
                    IsADAuthRequired = user.IsADAuthRequired,
                    IsCoreIntegration = user.IsCoreIntegration,
                    RoleIds = user.UserRoles.Select(x => x.RoleId).ToList(),
                    CustomerGroupIds = user.CustomerGroups.Select(x => x.CustomerGroupId).ToList(),
                    UserOptions = user.UserOptions.Select(x => new UserOptionsVM
                    {
                       // Id = x.Id,
                        ControlType = x.ControlType,
                        IsEditable = x.IsEditable,
                        IsVisible = x.IsVisible,
                        PropertyName = x.PropertyName,
                        Value = x.Value
                    }).ToList(),
                    DefaultShippingAddressId = user.DefaultShippingAddressId
                }).FirstOrDefaultAsync();
            return user;


        }
        public async Task<UserGridOutputVM<UserViewModel>> GetList(UserSearchTableParam<UserListSearchVM> param,GeneralEnums.FillterType? fillterType)
        {
            UserListSearchVM search = param.Search;
          
            var UserQuery = await _userManager.Users
                .AsNoTracking()
                .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
                .Include(x => x.CustomerGroups)
                .ThenInclude(x => x.CustomerGroup)
                .Where(user =>
                (string.IsNullOrEmpty(search.Name) || (user.FullName.ToUpper().Contains(search.Name.ToUpper()) || user.UserName.ToUpper().Contains(search.Name.ToUpper())))
                && ((search.GroupId == null || search.GroupId == 0) || user.CustomerGroups.Any(r => r.CustomerGroupId == search.GroupId))
                && ((search.RoleId == null || search.RoleId == 0) || user.UserRoles.Any(r => r.RoleId == search.RoleId))
                && (search.Status == null || search.Status == 0 || user.Status == search.Status)
                 ).Select(x => new UserFilteVM
                 { 
                    
                     CreatedById = x.CreatedById,
                     FullName = x.FullName,
                     CreationDate = x.CreationDate,
                     ModifiedById = x.ModifiedById,
                     ModificationDate = x.ModificationDate,
                     UserRoles = x.UserRoles,
                     CustomerGroups = x.CustomerGroups,
                     UserSites = x.UserSites,
                     CustomerGroupId = x.CustomerGroups.Select(x => x.CustomerGroupId).FirstOrDefault(),
                     RoleId = x.UserRoles.Select(x => x.RoleId).FirstOrDefault(),
                     UserName = x.UserName,
                     Email=x.Email,
                     UserId=x.Id,
                     Status=x.Status,
                     
                    
                 }).ToListAsync();
             

            var QueryWithOrder = new List<UserFilteVM>();
            if (fillterType == FillterType.Name)
            {
               QueryWithOrder = UserQuery.OrderBy(x => x.FullName.ToLower()).ToList();
            }
            else if (fillterType == FillterType.UserRole)
            {
                QueryWithOrder = UserQuery.OrderBy(x => x.RoleId).ToList();
            }
            else if (fillterType == FillterType.Usergroup)
            {
                QueryWithOrder = UserQuery.OrderBy(x => x.CustomerGroupId).ToList();
            }
            else if (fillterType == null)
            {
                QueryWithOrder = UserQuery.OrderByDescending(x => x.CreationDate).ToList();

            }
            UserGridOutputVM<UserViewModel> User = new UserGridOutputVM<UserViewModel>
            {
                Items = QueryWithOrder.Select(p => new UserViewModel
                {
                   
                    UserId = p.UserId,
                    FullName = p.FullName,
                    UserName = p.UserName,
                    UserGroup = p.CustomerGroups.Where(x => x.UserId == p.UserId).Select(x => x.CustomerGroup.Name).ToList(),
                    RoleName  =  p.UserRoles.Where(x => x.UserId == p.UserId).Select(x => x.Role.Name).ToList(),
                    Status = p.Status,
                    StatusDesc = Enum.GetName(typeof(EStatus), p.Status),
                    LastLogin = p.LastLoginDate.HasValue ? (DateTimeOffset)p.LastLoginDate : null,
                    Email = p.Email,
                    RoleId=p.UserRoles.Where(x => x.UserId == p.UserId).Select(x => x.Role.Id).ToList(),
                }).Skip(param.Pagination.Number * (param.Pagination.Start - 1)).Take(param.Pagination.Number)
                   .OrderByDescending(x=>x.CreationDate).ToList(),
                TotalRecord = UserQuery.Count
                
            };
            

            return User;
        }

        public async Task<List<UserGetAllVM>> GetAll()
        {
            List<UserGetAllVM> UsersQuery = await _userManager.Users
                .Where(user => user.Status == EStatus.Active).OrderByDescending(x => x.CreationDate).Select(x => new UserGetAllVM
                {
                    Id = x.Id,
                    Name = x.FullName,
                    PhoneNumber = x.PhoneNumber,
                    UserName = x.UserName
                }).ToListAsync();

            return UsersQuery;
        }
        public async Task<List<QuickSearchVM>> QuickSearch(UserSearchOption searchOption, long siteId)
        {
            var query = _userSitesRepository.Query().Include(x => x.User).Where(x => x.SiteId == siteId && (x.User.Status != EStatus.Deleted));
            if (!string.IsNullOrWhiteSpace(searchOption.Name))
            {
                EUsersTypes userTypes = searchOption.UserType;
                if (searchOption.UserType == null || searchOption.UserType == 0)
                    query = query.Where(x => x.User.FullName.ToLower().Contains(searchOption.Name.ToLower())
                    || x.User.UserName.ToLower().Contains(searchOption.Name.ToLower())
                    || x.User.Email.ToLower().Contains(searchOption.Name.ToLower()));
                else
                    query = query.Where(x => (x.User.FullName.ToLower().Contains(searchOption.Name.ToLower()) || x.User.UserName.ToLower().Contains(searchOption.Name.ToLower())) && x.User.Type == searchOption.UserType);

            }

            List<QuickSearchVM> users = await query.Take(10).Select(x => new QuickSearchVM
            {

                Id = x.Id,
                UserId = x.UserId,
                Email = x.User.Email,
                FullName = x.User.FullName,
                UserName = x.User.UserName,
                PhoneNumber = x.User.PhoneNumber,
                LastLoginDate = (DateTimeOffset)x.User.LastLoginDate,
                Roles = x.User.UserRoles.Select(x => x.Role.Name).ToList(),
                UserGroup = x.User.CustomerGroups.Select(x => x.CustomerGroup.Name).ToList()
            }).ToListAsync();

            return users;
        }




        public async Task<bool> SyncADUsers()
        {
            try
            {
                Task.Run( () => SyncActiveDirectoryUsers()).ConfigureAwait(false); 
             
                   
                return true;
            }
            catch
            {
                return false;
            }
        }



        private async Task<List<ADUser>> GetActiveDirectoryUsers()
        {
            try
            {
                var credentialsCache = new NetworkCredential("w.abuaishe", "Eska123");
                var handler = new HttpClientHandler { Credentials = credentialsCache };
                using (var client = new HttpClient(handler))
                {
                    client.BaseAddress = new Uri("http://eska-intranet:2021/");
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
                    //GET Method  
                    HttpResponseMessage response = await client.GetAsync("api/Identity/GetDomainUsers");
                    string responseContent = response.Content.ReadAsStringAsync().Result;
                    EskaCoreBaseResponseVM<List<ADUser>> returnResponse = JsonSerializer.Deserialize<EskaCoreBaseResponseVM<List<ADUser>>>(responseContent);
                    return returnResponse.content;
                }
            }
            catch (Exception e)
            {

                throw e;
            }

        }

        private async Task  SyncActiveDirectoryUsers()
        {
            if (SyncingUsersStarted == true)
                return;

            try
            {
                SyncingUsersStarted = true;
                // This using statment is used for retreving disposed objects
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var UserRepoService = scope.ServiceProvider.GetRequiredService<IRepository<User>>();
                    var groupRepo = scope.ServiceProvider.GetRequiredService<IRepository<CustomerGroup>>();
                    var eskaCoreIntegrationService = scope.ServiceProvider.GetRequiredService<IEskaCoreIntegrationService>();

                    List<ADUser> adUsers = await GetActiveDirectoryUsers();
                    string token = eskaCoreIntegrationService.Authenticate().Result.content.Token;

                    foreach (var adUser in adUsers)
                    {
                        User user = UserRepoService.Query().Include(x => x.UserRoles).Where(x => x.UserName == adUser.samAccountName).FirstOrDefault();
                        HREmployee employee = await GetEmployee(adUser.samAccountName);
                        if (employee != null)
                        {
                            adUser.voiceTelephoneNumber = employee.MOBILE;
                        }
                        else
                        {
                            Random generator = new Random();
                            String r = generator.Next(0, 1000000).ToString("D6");
                            adUser.voiceTelephoneNumber = "0781" + r;
                        }
                        var userRoles = user.UserRoles;
                        try
                        {
                            if (user == null)
                            {
                                await CreateUser(adUser, groupRepo, UserRepoService);
                            }
                            else
                            {
                                InsertUserBranchVM insertUserBranchVM = new InsertUserBranchVM
                                {
                                    CRG_COM_ID = 1,
                                    CRG_BRN_ID = 1,
                                    Username = user.UserName,
                                    CREATED_BY = "DCMS Background service",
                                    AuthToken = token
                                };
                                List<HREmployee> employees = await GetEmployees(adUser.samAccountName);
                                if (user.UserRoles.Count <= 0)
                                {
                                    user.UserRoles = AddUserRoles(6);
                                    UserRepoService.SaveChanges();
                                }
                                
                                foreach (var empl in employees)
                                {
                                    if (!string.IsNullOrEmpty(empl.USERNAME))
                                    {
                                        User emp = UserRepoService.Query().Where(x => x.UserName == empl.USERNAME).FirstOrDefault();
                                        emp.ParentId = user.Id;
                                        UserRepoService.SaveChanges();
                                        string adUserDepartment = adUser.distinguishedName.Split(',')[1];
                                        string department = adUserDepartment.Split('=')[1];
                                        CustomerGroup customerGroup = groupRepo.Query().Where(x => x.Name == department).FirstOrDefault();

                                        if (customerGroup != null)
                                        {
                                            //customerGroup.Description=
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception Exep)
                        {
                            user.UserRoles = userRoles;
                            continue;
                        }
                    }
                }

            }
            catch( Exception e)
            {
               
                // nothing to do here 
            }
            SyncingUsersStarted = false;
        }



        private async Task<HREmployee> GetEmployee(string username)
        {
            using (var client = new HttpClient())
            {
                List<HREmployee> respone = new List<HREmployee>();
                client.BaseAddress = new Uri("http://imsserver/AdministrationAPI/JsonServer/Intranet/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
                //GET Method  
                HttpResponseMessage response = await client.GetAsync("GetEmployees?CompanyId=1&PageNumber=1&RowsCountPerPage=&EmployeeName=&EmployeeNo=&nBranch=1&nDepartment=&EmployeeStatus=2");
                string responseContent = response.Content.ReadAsStringAsync().Result;
                CoreResponse<List<HREmployee>> returnResponse = JsonSerializer.Deserialize<CoreResponse<List<HREmployee>>>(responseContent);
                HREmployee employee = returnResponse.content.Where(x => x.USERNAME == username).FirstOrDefault();

                return employee;
            }

        }
        private async Task<List<HREmployee>> GetEmployees(string username)
        {
            using (var client = new HttpClient())
            {
                List<HREmployee> respone = new List<HREmployee>();
                client.BaseAddress = new Uri("http://imsserver/AdministrationAPI/JsonServer/Intranet/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
                //GET Method  
                HttpResponseMessage response = await client.GetAsync("GetEmployees?CompanyId=1&PageNumber=1&RowsCountPerPage=&EmployeeName=&EmployeeNo=&nBranch=1&nDepartment=&EmployeeStatus=2");
                string responseContent = response.Content.ReadAsStringAsync().Result;
                CoreResponse<List<HREmployee>> returnResponse = JsonSerializer.Deserialize<CoreResponse<List<HREmployee>>>(responseContent);
                //Core.Models.HREmployee employee = returnResponse.Data.Where(x => (x.USERNAME != null) && (x.USERNAME.ToUpper() == username.ToUpper())).FirstOrDefault();
               HREmployee employee = returnResponse.content.Where(x => x.USERNAME == username).FirstOrDefault();
                if (employee != null)
                {
                    List<HREmployee> employees = returnResponse.content.Where(x => x.MANAGER_ID == employee.ID).ToList();
                    return employees;
                }
                return respone;
            }

        }

        private async Task CreateUser(ADUser adUser, IRepository<CustomerGroup> groupRepository, IRepository<User> userRepository)
        {


            try
            {
                User user = new User    
                {
                    UserName = adUser.samAccountName,
                    Email = adUser.emailAddress,
                    FullName = adUser.displayName,
                    Type = EskaCMS.Core.Enums.GeneralEnums.EUsersTypes.Customer,
                    CreatedById = 1,
                    IsADAuthRequired = true,
                    IsCoreIntegration = false,
                    PhoneNumber = adUser.voiceTelephoneNumber,
                    Status = EskaCMS.Core.Enums.GeneralEnums.EStatus.Active,
                    ConcurrencyStamp = "05286794-6c65-4c3e-8de2-e6a10b85d050",
                    PasswordHash = "AQAAAAEAACcQAAAAELIGzYdxN6Giv7M0KV3bOlPZanDbaOH+u71HsLZp1d8sNf0lOhnP+Kb4zRt7C1h7Dw==",
                    PhoneNumberConfirmed = false,
                    SecurityStamp = "AQAAAAEAACcQAAAAELIGzYdxN6Giv7M0KV3bOlPZanDbaOH+u71HsLZp1d8sNf0lOhnP+Kb4zRt7C1h7Dw=="
                };
                string adUserDepartment = adUser.distinguishedName.Split(',')[1];
                string department = adUserDepartment.Split('=')[1];
                CustomerGroup customerGroup = groupRepository.Query().Where(x => x.Name == department).FirstOrDefault();
                user.UserRoles = AddUserRoles(6);
                if (customerGroup == null)
                {
                    long groupId = CreateGroup(department, groupRepository);
                    user.CustomerGroups = AddUserGroups(groupId);
                }
                else
                {
                    user.CustomerGroups = AddUserGroups(customerGroup.Id);
                }
                userRepository.Add(user);
                userRepository.SaveChanges();
                UsersSites UserToSite = new UsersSites { UserId = user.Id, SiteId = 1, IsDefault = true };
                _userSitesRepository.Add(UserToSite);
                _userSitesRepository.SaveChanges();
                Site site = _userSitesRepository.Query().Include(x => x.SiteId).Where(x => x.SiteId == 1).Select(x => x.Site).FirstOrDefault();
                if (site.IsCoreIntegration)
                {
                    user.IsCoreIntegration = true;
                    userRepository.SaveChanges();
                    EskaCoreBaseResponseVM<List<CoreGroupVM>> userInfo = await _eskaCoreIntegrationService.GetUserInfo("", user.UserName);
                    if (userInfo.content.Count == 0)
                    {
                        UserForm userForm = new UserForm
                        {
                            Email = adUser.emailAddress,
                            FullName = adUser.displayName,
                            UserName = adUser.samAccountName,
                            Password = "Eska@123"
                        };
                        await _eskaCoreIntegrationService.InsertUser(userForm);
                    }
                }
            }
            catch (Exception e)
            {

                throw e;
            }
        }

        private static List<CustomerGroupUser> AddUserGroups(long groupId)
        {
            List<CustomerGroupUser> userGroups = new List<CustomerGroupUser>();
            CustomerGroupUser userCustomerGroup = new CustomerGroupUser
            {
                CustomerGroupId = groupId
            };

            userGroups.Add(userCustomerGroup);
            return userGroups;
        }

        private static List<UserRole> AddUserRoles(long roleId)
        {
            List<UserRole> userRoles = new List<UserRole>();
            UserRole userRole = new UserRole
            {
                RoleId = roleId
            };
            userRoles.Add(userRole);
            return userRoles;
        }

        private long CreateGroup(string name, IRepository<CustomerGroup> groupRepository)
        {
            CustomerGroup group = new CustomerGroup
            {
                CreatedOn = DateTime.Now,
                Description = name,
                IsActive = true,
                IsDeleted = false,
                LatestUpdatedOn = DateTime.Now,
                Name = name,
                SiteId = 1,
                FireBaseTopic = "ADDepartment"
            };
            groupRepository.Add(group);
            groupRepository.SaveChanges();

            return group.Id;
        }




    }

}
