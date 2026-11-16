namespace BlogCms.Web.Models;

/// <summary>
/// Anzeigetext der KI-Kennzeichnung am Beitragskopf und in der Fusszeile.
///
/// <para>Die Kennzeichnung muss nach Artikel 50 Absatz 5 der KI-Verordnung klar
/// und deutlich sein und spätestens beim ersten Kontakt mit dem Beitrag
/// vorliegen. Deshalb steht der Text am Textanfang und nicht am Ende; der Link
/// auf die Hinweis-Seite ist Zusatz, nicht Ersatz.</para>
/// </summary>
/// <param name="Text">Der angezeigte Hinweistext.</param>
/// <param name="ShowLink">
/// <c>true</c>, wenn der Hinweis selbst auf die Hinweis-Seite verlinken soll.
/// In der Fusszeile steht der Link bereits in der Linkzeile, dort ist er
/// <c>false</c>, damit derselbe Link nicht doppelt erscheint.
/// </param>
public sealed record KiKennzeichnungModel(string Text, bool ShowLink = true);
