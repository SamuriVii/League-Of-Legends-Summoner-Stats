using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LeagueInfo.Pages
{
    public class IndexModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public string GameName { get; set; }

        [BindProperty(SupportsGet = true)]
        public string TagLine { get; set; }

        public IActionResult OnGet()
        {
            if (!string.IsNullOrWhiteSpace(GameName) && !string.IsNullOrWhiteSpace(TagLine))
            {
                return RedirectToPage("PlayerStats", new { gameName = GameName, tagLine = TagLine });
            }
            return Page();
        }
    }
}
