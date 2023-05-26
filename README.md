## Credify - An IW4MAdmin Plugin

Credify is an engaging betting system for IW4MAdmin that lets players gamble their in-game credits on team or player outcomes. Players can also participate in mindless gambling using their credits.

When new players join, they receive credits equal to their total kills (if any), otherwise they start with 0 credits. Each kill grants 1 credit. Betting requires a minimum of 10 players.

Credits are global, not server-specific, and can be viewed with the !crtop command. However, team/player bets are server-specific.

## Commands
### User Commands

```
  !crhelp - Displays help for the plugin
  
  !cr <optional name> - Returns yours or someone's credits
  !crpay <name> <amount> - Pay someone credits
  !crstats - Displays global credits statistics
  !crtop - List top players by credits
  
  !crsl - Displays the current lotto pot and who has entered
  !crlotto <amount> - Buy lotto ticket(s) for a chance to win the pot
  
  !crbet <amount> - Gamble credits in a tiered win/loss system
  !crbp <name> <amount> - Bet on a player to win - Payout is proportionate based on their ELO
  !crbt <team> <amount> - Bet on a team to win - Payout is proportionate based team's average ELO
  !crcnb - Cancel your open bets
  !crcb - Claim any expired/completed bets you've previously made. (No credits lost if unclaimed)
  !crob - Lists current bets on your server. (ID - Origin - Target - Amount)
  !crcnb - Cancels your open bets
  !crrps - Play rock paper scissors
  
  !crshop - Displays the shop
  !crbuy <ID> - Buy an item from the shop
  !crinv - Displays your inventory
```
  - Use "all" instead of an amount to stake everything.
  
### Admin Commands
```
!crset <name> <amount> - Set a player's credits
!crrb - Shows the recent shop buys
```

## Requirements
.NET 6

IW4MAdmin v2023.4.15.3 or later.
