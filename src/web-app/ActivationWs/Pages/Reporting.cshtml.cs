using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ActivationWs.Data;
using ActivationWs.Models;
using Microsoft.Extensions.Logging;

namespace ActivationWs.Pages
{
    public class ReportingModel : PageModel
    {
        private readonly ActivationDbContext _context;
        private readonly ILogger<ReportingModel> _logger;

        public ReportingModel(ActivationDbContext context, ILogger<ReportingModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        public IList<Machine> Machines { get; set; } = new List<Machine>();
        
        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;
        
        public int TotalPages { get; set; }
        public const int PageSize = 20;

        public async Task OnGetAsync()
        {
            try
            {
                var query = _context.Machines
                    .Include(m => m.ActivationRecords.OrderByDescending(ar => ar.LicenseAcquisitionDate))
                    .AsQueryable();

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(SearchTerm))
                {
                    query = query.Where(m => m.Hostname.Contains(SearchTerm));
                }

                // Calculate total pages
                var totalCount = await query.CountAsync();
                TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

                // Apply pagination
                Machines = await query
                    .OrderBy(m => m.Hostname)
                    .Skip((PageNumber - 1) * PageSize)
                    .Take(PageSize)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} machines for reporting (Page {Page} of {Total})", 
                    Machines.Count, PageNumber, TotalPages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading reporting data");
                Machines = new List<Machine>();
            }
        }
    }
}
