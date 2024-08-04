**How It Works (Conceptually):**

1. **Command Initiation:** A player initiates a blackjack game by typing a command like `!blackjack start` or `!blackjack bet <amount>`.
2. **Command Handling:** The `BlackjackCommand` handles the input, parsing the command arguments and delegating to the `BlackjackGameManager`.
3. **Game Management:** The `BlackjackGameManager` manages game state, deals cards, evaluates hands, and determines winners. It provides methods for players to interact with the game (hit, stand, etc.)
4. **Chat Interaction:** The `BlackjackGameManager` sends messages to the player via in-game chat, displaying their hand, the dealer's hand, and the outcome of the game.
5. **Persistence (Optional):** Player money and game progress could be persisted using the `IMetaServiceV2`, allowing players to continue their games across sessions.

**Remember:**  This is just a high-level skeleton. You'll need to fill in the actual logic within each method to implement the game mechanics.

**Tips:**

* **Start Simple:** Begin with a basic implementation of the core game rules before adding features like splitting and insurance.
* **Input Validation:** Carefully validate player input to prevent errors and exploits.
* **Testing:** Thoroughly test each feature to ensure it works as expected.

Good luck building your in-game blackjack plugin! 


---

You are absolutely correct! I got focused on completing the `Table` class and missed the updates needed for the other classes to support the new features like double down, split, surrender, insurance etc.

Here are the updated files with the necessary changes:

**Player.cs**

```csharp
using SharedLibraryCore.Database.Models;

namespace Credify.ChatGames.Blackjack.Rewrite;

public class Player(EFClient client)
{
    public EFClient Client { get; } = client;
    public List<Hand> Hands { get; set; } = [];
}
```

**TableManager.cs**

```csharp
﻿namespace Credify.ChatGames.Blackjack.Rewrite;

public class TableManager(PersistenceManager persistenceManager, Translations translations)
{
    private Table? _table;
    public async Task StartGame(CancellationToken cancellationToken = default)
    {
        _table = new Table(persistenceManager, translations);
        await _table.GameLoopAsync(cancellationToken);
    }

    public void AddPlayer(EFClient client)
    {
        Table.ThrowIfDeckIsNotInitialised(_table);
        _table.AddPlayer(client);
    }

    public void RemovePlayer(EFClient client)
    {
        Table.ThrowIfDeckIsNotInitialised(_table);
        var player = _table._players.FirstOrDefault(x => x.Key == client).Value;
        if (player is null) return;
        _table.RemovePlayer(player);
    }
}
```

**Card.cs**

```csharp
using Credify.ChatGames.Blackjack.Rewrite.Enums;

namespace Credify.ChatGames.Blackjack.Rewrite;

public class Card(CardRank rank, CardSuit suit)
{
    public CardSuit Suit { get; } = suit;
    public CardRank Rank { get; } = rank;

    public int Value => Rank switch
    {
        CardRank.Ace => 11,
        CardRank.Two => 2,
        CardRank.Three => 3,
        CardRank.Four => 4,
        CardRank.Five => 5,
        CardRank.Six => 6,
        CardRank.Seven => 7,
        CardRank.Eight => 8,
        CardRank.Nine => 9,
        CardRank.Ten => 10,
        CardRank.Jack => 10,
        CardRank.Queen => 10,
        CardRank.King => 10,
        _ => throw new InvalidOperationException("Invalid card rank")
    };

    public override string ToString()
    {
        return $"{Rank} of {Suit}";
    }
}
```

**Deck.cs**

```csharp
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Credify.ChatGames.Blackjack.Rewrite.Enums;

namespace Credify.ChatGames.Blackjack.Rewrite;

public class Deck
{
    private ConcurrentQueue<Card> _cards = ReplenishDeck();

    public Card DealCard()
    {
        if (_cards.IsEmpty) _cards = ReplenishDeck();

        if (!_cards.TryDequeue(out var card)) throw new InvalidOperationException("Deck is empty");

        return card;
    }

    private static ConcurrentQueue<Card> ReplenishDeck()
    {
        var cards = CreateDeck();
        return new ConcurrentQueue<Card>(Shuffle(cards));
    }

    private static List<Card> CreateDeck()
    {
        var cards = new List<Card>();
        foreach (var suit in Enum.GetValues<CardSuit>())
        {
            foreach (var rank in Enum.GetValues<CardRank>())
            {
                cards.Add(new Card(rank, suit));
            }
        }

        return cards;
    }

    private static IEnumerable<Card> Shuffle(IEnumerable<Card> cards) => cards.OrderBy(_ => Guid.NewGuid());

    public static void ThrowIfDeckIsNotInitialised([NotNull] Deck? deck)
    {
        if (deck is null) throw new InvalidOperationException("Deck is not initialized.");
    }
}
```

**Hand.cs**

```csharp
using System.Diagnostics.CodeAnalysis;
using Credify.ChatGames.Blackjack.Rewrite.Enums;

namespace Credify.ChatGames.Blackjack.Rewrite;

public class Hand(List<Card> initialCards, int bet = default)
{
    public List<Card> Cards { get; } = initialCards;
    public int Bet { get; set; } = bet;
    public bool IsDoubledDown { get; set; }
    public bool IsInsured { get; set; }
    public bool IsSurrendered { get; set; }
    public GameOutcome Outcome { get; set; }

    public int GetHandValue()
    {
        var aceCount = Cards.Count(x => x.Rank is CardRank.Ace);
        var totalValue = Cards.Sum(card => card.Value);

        while (totalValue > 21 && aceCount > 0)
        {
            totalValue -= 10;
            aceCount--;
        }

        return totalValue;
    }

    public bool IsBlackjack() => Cards.Count is 2 && GetHandValue() is 21;
    
    public static void ThrowIfDeckIsNotInitialised([NotNull] Hand? hand)
    {
        if (hand is null) throw new InvalidOperationException("Hand is not initialized.");
    }
}
```

**HandleInput.cs**

```csharp
﻿using SharedLibraryCore;

// ReSharper disable AccessToDisposedClosure

namespace Credify.ChatGames.Blackjack.Rewrite;

public class HandleInput(PersistenceManager persistenceManager)
{
    public record PlayerBet(Player Player, int Bet);

    public async Task<List<PlayerBet>> GetPlayerBetsAsync(List<Player> players, CancellationToken token)
    {
        using var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(tokenSource.Token, token);

        var tasks = players.Select(player => Task.Run(async () =>
        {
            var bet = -1;
            await player.Client.PromptClientInput("[BJ] How much do you want to bet?", async input =>
            {
                if (!int.TryParse(input, out bet)) return false;
                var credits = await persistenceManager.GetClientCreditsAsync(player.Client);
                return Math.Abs(bet) <= credits;
            }, linkedTokenSource.Token);

            return new PlayerBet(player, bet);
        }, linkedTokenSource.Token));

        var completedTasks = await Task.WhenAll(tasks);

        return completedTasks.Where(x => x.Bet is not -1).ToList();
    }
}
```

**HandleOutput.cs**

```csharp
﻿namespace Credify.ChatGames.Blackjack.Rewrite;

public class HandleOutput(Translations translations)
{
    public void MessagePlayer(Player player, string message) => player.Client.Tell($"{Translations.Blackjack.TitleShort} {message}");
}
```

**Enums/GameState.cs**

```csharp
﻿namespace Credify.ChatGames.Blackjack.Rewrite.Enums;

public enum GameState
{
    WaitingForPlayers,
    WaitingForBets,
    WaitingForAction,
    Playing,
}
```

**Enums/GameOutcome.cs**

```csharp
﻿namespace Credify.ChatGames.Blackjack.Rewrite.Enums;

public enum GameOutcome
{
    Blackjack,
    Win,
    Lose,
    Push,
}
```

These changes enable the features mentioned earlier and improve the overall structure of the Blackjack game. Please let me know if you have any other questions. 
