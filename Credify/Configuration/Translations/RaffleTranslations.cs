namespace Credify.Configuration.Translations;

public class RaffleTranslations
{ 
    // @formatter:off
    public string ShowDescription { get; set; } = "Shows the current raffle holders";
    public string Description { get; set; } = "Buy your raffle ticket!";
    private string PrefixLong { get; set; } = "[(Color::Pink)Raffle(Color::White)]";
    public string Success { get; set; } = "(Color::Accent)You have purchased ticket {{ticket}}! (Color::Green)Good luck!";
    public string ClientAlreadyPurchased { get; set; } = "(Color::Yellow)You have already purchased a ticket!";
    public string TicketAlreadyPurchased { get; set; } = "(Color::Yellow)Ticket {{ticket}} has already been purchased!";
    public string InvalidTicketRange { get; set; } = "(Color::Red)Invalid ticket range! (Color::White)0-1000";
    public string RaffleNotStarted { get; set; } = "(Color::Yellow)The raffle has not started yet!";
    public string ShowRaffleHeader { get; set; } = "(Color::Accent)--Ticket Holders--";
    public string RaffleNextDraw { get; set; } = "Next draw in (Color::Accent){{nextDrawHumanized}}";
    public string NoTicketHolders { get; set; } = "(Color::Yellow)No ticket holders. (Color::White)Buy some tickets! (Color::Accent)!crraf";
    public string NoTicketHoldersContinued { get; set; } = "(Color::White)Bank Amount: (Color::Green)${{bankCredits}} (Color::White)- Next Draw: (Color::Accent){{nextDraw}}";
    public string AnnounceRaffleWinner { get; set; } = "(Color::Accent){{cleanedName}} (Color::White)won (Color::Green)${{bankCredits}} (Color::White)from the raffle with a (Color::Accent){{winPct}}(Color::White)pct chance!";
    public string NoLastWinner { get; set; } = "(Color::Accent)Good luck!";
    public string PreviousRaffleCount { get; set; } = "(Color::White)Previous total players (Color::Accent){{playerCount}}";
    public string LastWinner { get; set; } = "Last winner (Color::Accent){{name}} (@{{clientId}}) (Color::White)won (Color::Green)${{winTotal}}(Color::White)!";
    public string TicketHolder { get; set; } = "[(Color::Accent)#{{ticket}}(Color::White)] {{name}}";
    public string ShowSummary { get; set; } = "(Color::Accent)--Raffle-- (Color::White)Pot (Color::Green)${{pot}} (Color::White)- (Color::Accent){{count}} (Color::White)entries";
    public string YourTicket { get; set; } = "(Color::White)Your ticket (Color::Accent)#{{ticket}} (Color::White)| Draw in (Color::Accent){{next}}";
    public string NoTicketYet { get; set; } = "(Color::White)No ticket yet - buy one: (Color::Accent)!crraf (Color::White)| Draw in (Color::Accent){{next}}";
    public string TicketsLabel { get; set; } = "(Color::Accent)Tickets:";
    public string MoreTickets { get; set; } = "(Color::White)...and (Color::Accent){{count}} (Color::White)more";
    // @formatter:on

    public string Prefix(string message) => $"{PrefixLong} {message}";
}
