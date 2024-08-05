## Credify - An IW4MAdmin Plugin

Credify is an engaging betting system for IW4MAdmin that lets players gamble their in-game credits on team or player outcomes. Players can also participate in mindless gambling using their credits.

When new players join, they receive credits equal to their total kills (if any), otherwise they start with 0 credits. Each kill grants 1 credit. Betting requires a minimum of 10 players.

Credits are global, not server-specific, and can be viewed with the !crtop command. However, team/player bets are server-specific.

## Commands
### User Commands

```
!crhelp - Displays help for the plugin

!cr <optional name> - Returns yours or someone's credits
!crpay <name> <amount> - Give someone your credits
!crstats - Displays global credits statistics
!crtop - List top players by credits

!crrl - Join the Roulette Table
!crbj - Join the Blackjack Table
!crrps - Play Rock Paper Scissors

!crsl - Displays the current lotto pot and who has entered
!crlotto <amount> - Buy lotto ticket(s) for a chance to win the pot



!crshop - Displays the shop
!crbuy <ID> - Buy an item from the shop
!crinv - Displays your inventory
```
- Use "all" instead of an amount to stake everything.
  
### Admin Commands
```
!crset <name> <amount> - Set a player's credits
!crrb - Check players recent shop purchases.
!crreset - Reset the entire credit system - USE WITH CAUTION
```

## Requirements
.NET 8

IW4MAdmin v2024.8.XX.X+ or later.
