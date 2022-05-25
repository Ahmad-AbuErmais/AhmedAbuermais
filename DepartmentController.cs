using EskaCMS.DocumentsNumbering.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EskaCms.CoreShared.Controllers
{
    [Route("api/Department/[action]")]
    public class DepartmentController : ControllerBase
    {
        private readonly IDocumentLoockupsService _documentLoockupsService;

        public DepartmentController(IDocumentLoockupsService documentLoockupsService)
        {
            _documentLoockupsService = documentLoockupsService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllDepartments()
        {
            try
            {
                return Ok(await _documentLoockupsService.GetAllDepartments());
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
