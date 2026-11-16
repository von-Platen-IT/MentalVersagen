using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages;

/// <summary>
/// Statische Seite mit den Hinweisen zu Inhalten und Urheberschaft.
///
/// <para>Die Seite traegt zwei Aufgaben: sie ordnet das Projekt als Kunst ein
/// (Kunst- und Meinungsfreiheit) und sie erklaert die Kennzeichnung nach
/// Artikel 50 der KI-Verordnung, auf die jeder Beitrag am Kopf verweist.</para>
/// </summary>
public class HinweiseModel : PageModel
{
    public void OnGet()
    {
    }
}
