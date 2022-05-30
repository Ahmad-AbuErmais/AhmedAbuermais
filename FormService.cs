using EskaCMS.Core.Entities;
using EskaCMS.Core.Extensions;
using EskaCMS.Core.Model;
using EskaCMS.Core.Models;
using EskaCMS.Core.Services;
using EskaCMS.EmailSender.SMTP.Areas.ViewModels;
using EskaCMS.EmailSender.SMTP.Services;
using EskaCMS.Forms.Entities;
using EskaCMS.Forms.Model;
using EskaCMS.Forms.Services.Interfaces;
using EskaCMS.Infrastructure.Data;
using EskaCMS.Infrastructure.Web.SmartTable;
using EskaCMS.Pages.Services.Interfaces;
using EskaCMS.Sites.Entities;
using EskaCommerce.Notifications.Areas.Notifications.ViewModels;
using EskaCommerce.Notifications.Notifiers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EskaCMS.Forms.Services
{
    public class FormService : IFormService
    {

        private readonly IRepository<FormBuilder> _FormBuilderRepository;
        private readonly IEmailSender _emailSender;
        private readonly UserManager<User> _userManager;
        private readonly IRepository<UsersSites> _UsersSitesRepository;
        private readonly SignInManager<User> _signInManager;
        private readonly ILogger _logger;
        private readonly ITestNotifier _testNotifier;
        private readonly IWorkContext _IWorkContext;
        private readonly IPages _IPages;
        private readonly IEmailTemplateService _emailTemplateService;
        private readonly ISiteSettingsService _siteSettingsService;
    
        public FormService(IRepository<FormBuilder> FormBuilderRepository
            , IEmailSender emailSender
            , ILoggerFactory loggerFactory,
           IRepository<UsersSites> UsersSitesRepository
            , UserManager<User> userManager
            , SignInManager<User> signInManager
            , ITestNotifier testNotifier
            , IWorkContext IWorkContext
           , IPages IPages,
           IEmailTemplateService emailTemplateService
            , ISiteSettingsService siteSettingsService)
        {

            _FormBuilderRepository = FormBuilderRepository;
            _emailSender = emailSender;
            _userManager = userManager;
            _UsersSitesRepository = UsersSitesRepository;
            _signInManager = signInManager;
            _logger = loggerFactory.CreateLogger<FormService>();
            _testNotifier = testNotifier;
            _IWorkContext = IWorkContext;
            _IPages = IPages;
            _emailTemplateService = emailTemplateService;
            _siteSettingsService = siteSettingsService;
            //_BundleDestination = Configuration.GetSection("CMSConfig").GetSection("BuilderDestination").Value;
            //  _WebDestination = Configuration.GetSection("CMSConfig").GetSection("WebDestination").Value;
        }
    


        public async Task SaveForm(long PageId, string ComponentSettingsId, string Culture, string FormData, IFormFileCollection files)
        {
            try
            {
                var SiteId = _IWorkContext.GetCurrentSiteId();
                var Settings = await _IPages.GetComponentsSettings(PageId, ComponentSettingsId, Culture);
                if (Settings.Count() > 0)
                {


                    object EmailsTo = null;
                    object FormName = string.Empty;
                    object FormEmailKey = string.Empty;
                    object EmailFrom = string.Empty;
                    object SenderModule=string.Empty;
                    object CustomerEmailTemplateId = string.Empty;
                    object AdminEmailTemplateId = string.Empty;
                    object AttacheFolder = string.Empty;

                    Settings.TryGetValue("emailFrom", out EmailFrom);
                    Settings.TryGetValue("emailsTo", out EmailsTo);
                    Settings.TryGetValue("formName", out FormName);
                    Settings.TryGetValue("formEmailKey", out FormEmailKey);

                    Settings.TryGetValue("formReciveSubmiterMessage", out CustomerEmailTemplateId);
                    Settings.TryGetValue("formReciveMessage", out AdminEmailTemplateId);
                    Settings.TryGetValue("folderAttach", out AttacheFolder);
                    Settings.TryGetValue("senderModule", out SenderModule);
                    
                    //string[] arr = EmailsTo.Cast<int>().ToArray();
                    var AdminEmails = JArray.Parse(EmailsTo.ToString());
                    var ObjFormBuilder = new FormBuilder();
                    // var EmailTemplateId = "Request a Phone Call";// await _IPages.GetComponentsSettings(PageId, ComponentSettingsId, Culture, "TemplateId");

                    string FormattedAdminEmailMessage = string.Empty;
                    string FormattedCustomerEmailMessage = string.Empty;
                    var formFiles = new List<FileViewModel>();
                    AttacheFolder = null ?? "";

                    var myJsonObject = JsonConvert.DeserializeObject<dynamic>(FormData);

                    EmailTemplateVM objCustomerEmailTemplate = new EmailTemplateVM();
                    if (CustomerEmailTemplateId != null)
                    {
                        objCustomerEmailTemplate = await _emailTemplateService.GetEmailTemplateById(CustomerEmailTemplateId.ToString());

                        ObjFormBuilder.SubmitterMailSubject = objCustomerEmailTemplate.Subject;
                        ObjFormBuilder.SubmitterMailMessage = setDataKeys(FormData, objCustomerEmailTemplate.EmailBody);

                        var emailsubmitter = (string)myJsonObject[FormEmailKey];
                        if (!string.IsNullOrEmpty(emailsubmitter))
                        {
                            ObjFormBuilder.SubmitterMailReceiver = emailsubmitter.ToString();

                            NotificationObject Notificationobj = new NotificationObject();
                            Notificationobj.MessageTitle = objCustomerEmailTemplate.Subject;
                            FormattedCustomerEmailMessage = setDataKeys(FormData, objCustomerEmailTemplate.EmailBody);
                            Notificationobj.MessageBody = FormattedCustomerEmailMessage;
                            Notificationobj.SendValue.Add(emailsubmitter);
                            Notificationobj.EmailOptions.Attachment = formFiles;
                            Notificationobj.Severity = 0;
                            Notificationobj.EmailOptions.TemplateId = objCustomerEmailTemplate.Id;// data.EmailSenderVM;
                            await _testNotifier.SendMessageAsync(NotificationType.Email, Notificationobj, SiteId);
                        }

                    }
                    EmailTemplateVM objAdminEmailTemplate = new EmailTemplateVM();
                    if (AdminEmailTemplateId != null)
                    {
                        objAdminEmailTemplate = await _emailTemplateService.GetEmailTemplateById(AdminEmailTemplateId.ToString());
                        FormName = null ?? objAdminEmailTemplate.Id;
                        ObjFormBuilder.FormName = FormName.ToString();

                        ObjFormBuilder.MailSender = EmailFrom.ToString();//appsettings

                        ObjFormBuilder.MailReceiver = string.Join(",", AdminEmails);

                        FormattedAdminEmailMessage = setDataKeys(FormData, objAdminEmailTemplate.EmailBody);

                        ObjFormBuilder.MailSubject = objAdminEmailTemplate.Subject;
                        ObjFormBuilder.MailMessage = FormattedAdminEmailMessage;

                    }
                    
                    EmailFrom = null ?? _siteSettingsService.GetEmailSmtpSettings(SiteId, SenderModule == null ? string.Empty:SenderModule.ToString()  ).Result.SenderEmail;

                    if (files.Count > 0)
                    {
                        var FolderName = "wwwroot\\Resources\\site_" + SiteId + "\\" + AttacheFolder;//data.AttacheFolder;
                        if (!Directory.Exists(FolderName))
                        {
                            Directory.CreateDirectory(FolderName);
                        }

                        var pathToSave = Path.Combine(FolderName.Trim());

                        for (int i = 0; i < files.Count; i++)
                        {
                            FileViewModel fileToSave = new FileViewModel();

                            var file = files[i];
                            var fileName = ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.Trim('"');
                            var path = pathToSave;
                            if (files.Count > 1)
                            {
                                path = pathToSave + "\\" + file.Name;
                            }
                            if (!Directory.Exists(path))
                            {
                                Directory.CreateDirectory(path);
                            }

                            var fullPath = Path.Combine(path.Trim(), fileName.Trim());
                            var dbPath = Path.Combine(FolderName, fileName);
                            fileToSave.fileName = fileName.Trim();
                            fileToSave.publicUrl = path.Trim();

                            using (var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
                            {
                                file.CopyTo(stream);
                                formFiles.Add(fileToSave);
                            }

                        }
                    }
                    ObjFormBuilder.FormData = FormData;

                    ObjFormBuilder.AttacheFolder = AttacheFolder.ToString();





                    ObjFormBuilder.SubmitionDate = DateTimeOffset.Now;
                    ObjFormBuilder.SiteId = SiteId;
                    ObjFormBuilder.FormFiles = JsonConvert.SerializeObject(formFiles);
                    ObjFormBuilder.MailSendStatus = false;
                    ObjFormBuilder.SubmitterMailSendStatus = false;
                    _FormBuilderRepository.Add(ObjFormBuilder);
                    await _FormBuilderRepository.SaveChangesAsync();

                    if (AdminEmails.Count() > 0)
                    {
                        NotificationObject Notificationobj = new NotificationObject();
                        Notificationobj.MessageTitle = objAdminEmailTemplate.Subject;
                        Notificationobj.MessageBody = FormattedAdminEmailMessage;
                        Notificationobj.SendValue = AdminEmails.ToObject<List<string>>();
                        Notificationobj.EmailOptions.Attachment = formFiles;
                        Notificationobj.Severity = 0;
                        Notificationobj.EmailOptions = new EmailSenderVM();// data.EmailSenderVM;
                        await _testNotifier.SendMessageAsync(NotificationType.Email, Notificationobj, SiteId);
                    }
                    ObjFormBuilder.MailSendStatus = true;
                    ObjFormBuilder.SubmitterMailSendStatus = true;

                    _FormBuilderRepository.Update(ObjFormBuilder);
                }
                else
                {
                    throw new Exception("No settings found");
                }
            }
            catch (Exception exc)
            {
                throw exc;
            }

        }

        private string setDataKeys(string formData, string mailMessage)
        {
            try
            {
                //JObject myJsonObjectddd = JObject.Parse(formData);
                var myJsonObject = JsonConvert.DeserializeObject<dynamic>(formData);




                while (mailMessage.IndexOf("${{") != -1)
                {
                    try
                    {
                        // if key =="All_Params" 
                        var start = mailMessage.IndexOf("${{");
                        var end = mailMessage.IndexOf("}}", start) + 2;
                        var key = mailMessage.Substring(start + 3, end - 2 - (start + 3));
                        var replace = mailMessage.Substring(start, end - start);
                        var value = myJsonObject[key];

                        if (value != null)
                        {
                            if (value.ToString().StartsWith("[") && value.ToString().EndsWith("]"))
                            {
                                var data = JArray.Parse(value.ToString());
                                List<string> replaceArray = new List<string>();
                                foreach (var item in data)
                                {
                                    if (item.value == true)
                                    {
                                        replaceArray.Add(Regex.Replace(item.dataLabel, @"\s", " ").Trim().ToString());
                                    }

                                }
                                value = string.Join(",", replaceArray);

                            }
                            mailMessage = mailMessage.Replace(replace, value.ToString());
                        }

                        else
                            mailMessage = mailMessage.Replace(replace, " ");

                    }
                    catch (Exception)
                    {
                        break;
                    }

                }
            }
            catch (Exception)
            {
                return mailMessage;
            }
            return mailMessage;

        }

        public GridOutputVM<FormDataViewModel> GetSubmittedForms(long siteId, SearchTableParam<FormDataViewModel> param)
        {

            FormDataViewModel search = param.Search;
            var query = _FormBuilderRepository.Query().Where(x => x.SiteId == siteId
                     && (search.SubmitionDateFrom == null || x.SubmitionDate >= search.SubmitionDateFrom)
                     && (search.SubmitionDateTo == null || x.SubmitionDate <= search.SubmitionDateTo)
                     && (search.MailSendStatus == false || search.MailSendStatus == search.MailSendStatus)
                     && (search.SubmitterMailSendStatus == false || search.SubmitterMailSendStatus == search.SubmitterMailSendStatus))
                .OrderByDescending(d => d.SubmitionDate)
                .ToList(); ;

            GridOutputVM<FormDataViewModel> result = new GridOutputVM<FormDataViewModel>
            {
                Items = query.Select(c => new FormDataViewModel
                {
                    Id = c.Id,
                    FormName = c.FormName,
                    MailSender = c.MailSender,
                    MailSendStatus = c.MailSendStatus,
                    SubmitionDate = c.SubmitionDate,
                    SubmitterMailSendStatus = c.SubmitterMailSendStatus,
                    FormData = c.FormData,
                    MailSubject = c.MailSubject,
                    MailReceiverstr = c.MailReceiver,
                    SubmitterMailReceiverstr = c.SubmitterMailReceiver

                }).Skip(param.Pagination.Number * param.Pagination.Start).Take(param.Pagination.Number).ToList(),
                TotalRecord = query.Count()
            };

            return result;
        }
    }
}

