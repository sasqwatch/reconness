using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReconNess.Core.Services;
using ReconNess.Entities;
using ReconNess.Web.Dtos;

namespace ReconNess.Web.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class SubdomainsController : ControllerBase
    {
        private readonly IMapper mapper;
        private readonly ISubdomainService subdomainService;
        private readonly ITargetService targetService;
        private readonly ILabelService labelService;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubdomainsController" /> class
        /// </summary>
        /// <param name="mapper"><see cref="IMapper"/></param>
        /// <param name="subdomainService"><see cref="ISubdomainService"/></param>
        /// <param name="targetService"><see cref="ITargetService"/></param>
        /// <param name="labelService"><see cref="ILabelService"/></param>
        public SubdomainsController(
            IMapper mapper,
            ISubdomainService subdomainService,
            ITargetService targetService,
            ILabelService labelService)
        {
            this.mapper = mapper;
            this.subdomainService = subdomainService;
            this.targetService = targetService;
            this.labelService = labelService;
        }

        // GET api/subdomains/{target}/{subdomain}
        [HttpGet("{targetName}/{subdomainName}")]
        public async Task<IActionResult> Get(string targetName, string subdomainName, CancellationToken cancellationToken)
        {
            var target = await this.targetService.GetByCriteriaAsync(t => t.Name == targetName, cancellationToken);
            if (target == null)
            {
                return BadRequest();
            }

            var subdomain = await this.subdomainService.GetAllQueryableByCriteria(s => s.Target == target && s.Name == subdomainName, cancellationToken)
                .Include(s => s.Notes)
                .Include(s => s.Services)
                .Include(s => s.ServiceHttp)
                    .ThenInclude(s => s.Directories)
                .Include(s => s.Labels)
                    .ThenInclude(s => s.Label)
                .FirstOrDefaultAsync(cancellationToken);

            if (subdomain == null)
            {
                return NotFound();
            }

            return Ok(this.mapper.Map<Subdomain, SubdomainDto>(subdomain));
        }

        // POST api/subdomains
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] SubdomainDto subdomainDto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            var target = await this.targetService.GetByCriteriaAsync(t => t.Name == subdomainDto.Target, cancellationToken);
            if (target == null)
            {
                return BadRequest();
            }

            if (await this.subdomainService.AnyAsync(s => s.Name == subdomainDto.Name && s.Target == target))
            {
                return BadRequest();
            }

            var newSubdoamin = await this.subdomainService.AddAsync(new Subdomain
            {
                Target = target,
                Name = subdomainDto.Name
            }, cancellationToken);

            return Ok(mapper.Map<Subdomain, SubdomainDto>(newSubdoamin));
        }

        // PUT api/subdomains/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(Guid id, [FromBody] SubdomainDto subdomainDto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            var subdomain = await this.subdomainService.GetAllQueryableByCriteria(a => a.Id == id, cancellationToken)
               .Include(a => a.Labels)
                   .ThenInclude(ac => ac.Label)
               .FirstOrDefaultAsync();

            if (subdomain == null)
            {
                return NotFound();
            }

            var editedSubdmain = this.mapper.Map<SubdomainDto, Subdomain>(subdomainDto);

            subdomain.Name = editedSubdmain.Name;
            subdomain.IsMainPortal = editedSubdmain.IsMainPortal;
            subdomain.Labels = await this.labelService.GetLabelsAsync(subdomain.Labels, subdomainDto.Labels.Select(l => l.Name).ToList(), cancellationToken);

            await this.subdomainService.UpdateAsync(subdomain, cancellationToken);

            return NoContent();
        }

        // PUT api/subdomains/label/{id}
        [HttpPut("label/{id}")]
        public async Task<IActionResult> AddLabel(Guid id, [FromBody] SubdomainLabelDto subdomainLabelDto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            var subdomain = await this.subdomainService.GetAllQueryableByCriteria(a => a.Id == id, cancellationToken)
               .Include(a => a.Labels)
                   .ThenInclude(ac => ac.Label)
               .FirstOrDefaultAsync();

            if (subdomain == null)
            {
                return NotFound();
            }

            var myLabels = subdomain.Labels.Select(l => l.Label.Name).ToList();
            myLabels.Add(subdomainLabelDto.Label);

            subdomain.Labels = await this.labelService.GetLabelsAsync(subdomain.Labels, myLabels, cancellationToken);

            await this.subdomainService.UpdateAsync(subdomain, cancellationToken);

            return NoContent();
        }

        // DELETE api/subdomains/{target}/{id}
        [HttpDelete("{targetName}/{id}")]
        public async Task<IActionResult> Delete(string targetName, Guid id, CancellationToken cancellationToken)
        {
            var target = await this.targetService.GetByCriteriaAsync(t => t.Name == targetName, cancellationToken);
            if (target == null)
            {
                return BadRequest();
            }

            var subdomain = await this.subdomainService.GetAllQueryableByCriteria(s => s.Target == target && s.Id == id, cancellationToken)
                .Include(s => s.Notes)
                .Include(s => s.Services)
                .FirstOrDefaultAsync(cancellationToken);

            if (subdomain == null)
            {
                return NotFound();
            }

            await this.subdomainService.DeleteSubdomainAsync(subdomain, cancellationToken);

            return NoContent();
        }
    }
}
