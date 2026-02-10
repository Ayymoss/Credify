# Credify - Enhanced Credit System for IW4MAdmin

Credify transforms your IW4MAdmin server with a comprehensive credit economy system. Players earn credits through gameplay, compete in casino-style games, complete challenging quests, shop for items, and participate in exciting events. Create a more engaging and rewarding experience that keeps players coming back!

## Features Overview

### Credit System
Players earn credits through various activities including kills, winning games, completing quests, achieving kill streaks, and participating in events. Credits can be used for gambling, purchasing shop items, placing bounties, and more.

### Active Games
Real-time casino games that run continuously:
- **Blackjack** - Classic card game where players compete against the dealer
- **Poker** - Texas Hold'em style poker with betting rounds
- **Roulette** - European roulette with a variety of betting options

### Simple Games
Quick gambling games for instant action:
- **Rock Paper Scissors** - Classic RPS with credit stakes
- **Coin Flip** - Heads or tails betting
- **Slots** - Three-reel slot machine with various symbols and multipliers

### Passive Chat Games
Automated mini-games that run periodically in chat:
- **Trivia** - Answer questions for credits
- **Math Test** - Solve math problems
- **Typing Test** - Type phrases quickly
- **Countdown** - Count down from a number
- **Acronym** - Guess what the acronym stands for
- **Complete The Word** - Finish incomplete words

### Shop System
Purchase virtual items using your credits. Each item has a cost, availability limit, and can be tracked in your inventory.

### Quest System
Daily and permanent quests reward players for completing in-game objectives. Track your progress and earn bonus credits.

### Raffle System
Periodic raffles where players purchase tickets for a chance to win the accumulated pot. Draws happen on a schedule.

### Bounty Contracts
Place bounties on other players. When a bounty target is eliminated, the bounty hunter receives the credits.

### Wheel of Fortune
Spin the wheel once per day for a chance to win credits, multipliers, or other rewards. Cooldown resets daily.

### Statistics & Leaderboards
Track server-wide credit activity including total earned, spent, and won. View the top credit holders on the leaderboard.

## Commands

### Credit Management

| Command | Alias | Description |
|---------|-------|-------------|
| `!credify [player]` | `!cr [player]` | Check your credits or another player's credits |
| `!credifypay <player> <amount>` | `!crpay <player> <amount>` | Pay credits to another player (minimum 10) |
| `!credifystats` | `!crstats` | View global credit statistics |
| `!credifytop` | `!crtop` | Display the top credit holders leaderboard |
| `!credifyhelp [category]` | `!crhelp [category]` | Display help information. Optionally specify a category (Games, Shop, Credits, etc.) |

### Active Games

Join real-time casino games. Use the same command to leave if already joined.

| Command | Alias | Description |
|---------|-------|-------------|
| `!credifyblackjack` | `!crbj` | Join or leave the Blackjack table |
| `!credifypoker` | `!crpk` | Join or leave the Poker table |
| `!credifyroulette` | `!crrl` | Join or leave the Roulette table |

### Simple Games

Quick gambling games for immediate action.

| Command | Alias | Description |
|---------|-------|-------------|
| `!creditsrps <rock\|paper\|scissors> <amount>` | `!crrps <r\|p\|s> <amount>` | Play Rock Paper Scissors. Use "rock"/"r", "paper"/"p", or "scissors"/"s" followed by your bet amount |
| `!creditcf <H\|T> <amount>` | `!crcf <H\|T> <amount>` | Flip a coin. Choose Heads (H) or Tails (T) and your bet amount |
| `!credifyslots <amount>` | `!crslots <amount>` | Play the slot machine. Bet your desired amount |

### Special Features

| Command | Alias | Description |
|---------|-------|-------------|
| `!credifywheel` | `!crwof` | Spin the Wheel of Fortune (once per day) |
| `!credifybounty <player> <amount>` | `!crbounty <player> <amount>` | Place a bounty on a player. The bounty is awarded when they are eliminated |
| `!credifybounties` | `!crbounties` | List all active bounties on the server |

### Shop

| Command | Alias | Description |
|---------|-------|-------------|
| `!credifyshop` | `!crshop` | Display all available items in the shop |
| `!credifybuy <item_id>` | `!crbuy <item_id>` | Purchase an item from the shop using its ID |
| `!credifyinventory [player]` | `!crinv [player]` | View your purchased items. Optionally check another player's inventory |

### Quests

| Command | Alias | Description |
|---------|-------|-------------|
| `!credifyquests` | `!crq` | Display your current quests and progress (daily and permanent) |

### Raffle

| Command | Alias | Description |
|---------|-------|-------------|
| `!credifyraffle [ticket_number]` | `!crraf [ticket_number]` | Purchase a raffle ticket. Optionally specify a ticket number |
| `!credifyshowraffle` | `!crsr` | View current raffle participants, pot amount, and next draw time |

### Administrator Commands

| Command | Alias | Permission | Description |
|---------|-------|------------|-------------|
| `!credifysetcredits <player> <amount>` | `!crset <player> <amount>` | Owner | Set a player's credits to a specific amount |
| `!credifyrecentbuys` | `!crrb` | Administrator | View a list of recent shop purchases (last month) |
| `!credifywheelreset <player>` | `!crwofreset <player>` | Owner | Reset the wheel cooldown for a player |
| `!credifyresetcredits <config_id>` | `!crreset <config_id>` | Owner | **USE WITH CAUTION:** Reset the entire credit system (requires configuration ID) |
| `!credifywheelsim` | `!crwheelsim` | Owner | Simulate wheel spins for statistical analysis (debug tool) |
| `!credifycallraffle` | `!crcallraffle` | Owner | Manually trigger a raffle draw immediately (debug tool) |

## Feature Details

### How Credits Work

Credits are earned through various activities:
- **Kills** - Earn credits for eliminating opponents
- **Kill Streaks** - Bonus rewards for consecutive kills
- **Winning Games** - Earn credits by winning Blackjack, Poker, Roulette, or simple games
- **Completing Quests** - Daily and permanent quests reward credits upon completion
- **Chat Games** - Participate in passive mini-games for bonus credits
- **Wheel of Fortune** - Daily spin can reward credits or multipliers
- **Donations** - Receive credits from other players

Credits can be spent on:
- Gambling games (Blackjack, Poker, Roulette, Slots, Coin Flip, Rock Paper Scissors)
- Shop items
- Raffle tickets
- Placing bounties on players
- Wheel of Fortune spins (uses your full balance)

### Active Games

**Blackjack**: Join a table and play against the dealer. Hit, stand, double down, or split to get as close to 21 as possible without going over. Games run continuously with multiple players.

**Poker**: Join a Texas Hold'em style poker table. Participate in betting rounds, make strategic decisions, and compete against other players for the pot.

**Roulette**: Place bets on numbers, colors, columns, dozens, or other combinations. The wheel spins at regular intervals, and winning bets pay according to the odds.

### Passive Chat Games

Chat games run automatically on a schedule:
- **Trivia** - Questions appear in chat; first correct answer wins credits
- **Math Test** - Solve mathematical equations quickly
- **Typing Test** - Type the given phrase as fast as possible
- **Countdown** - Count down from the displayed number
- **Acronym** - Guess what the displayed acronym stands for
- **Complete The Word** - Finish the incomplete word

### Quests

Quests come in two types:
- **Daily Quests** - Reset each day with new objectives
- **Permanent Quests** - Long-term objectives that don't reset

Complete objectives such as getting kills, achieving kill streaks, spending credits, winning games, or other in-game actions to earn reward credits.

### Bounty System

Place bounties on other players by spending credits. When a bounty target is eliminated, the bounty hunter receives the full bounty amount. Multiple bounties can accumulate on the same player. Bounties are tracked and displayed via the `!credifybounties` command.

### Wheel of Fortune

Spin the wheel once per 24-hour period (resets at midnight). The wheel uses your full credit balance as the stake and can reward:
- Credit multipliers
- Fixed credit amounts
- Percentage-based rewards
- Or potentially result in losses

Rewards and risks are balanced based on your current balance.

### Shop & Inventory

Browse available items with `!credifyshop`, purchase items by ID with `!credifybuy`, and track your purchases with `!credifyinventory`. Items may have purchase limits and are stored in your personal inventory.

### Raffle System

Periodic raffles allow players to purchase tickets for a chance to win a large pot. Tickets can be purchased automatically (assigned number) or with a specific ticket number if available. The raffle draws a winner on a scheduled basis, and all ticket holders have a chance to win based on ticket distribution.

## Installation

Credify is available through the IW4MAdmin Plugin Store. Simply subscribe to the plugin via the [IW4MAdmin Plugin Store](https://store.raidmax.org/), and it will be automatically installed and updated.

The plugin offers extensive configuration options to customize:
- Credit earning rates and methods
- Game rules and payouts
- Shop items and pricing
- Quest objectives and rewards
- Raffle schedules and ticket costs
- Bounty fees and mechanics
- Wheel of Fortune segments and rewards
- Chat game frequency and rewards
- And much more!

Adjust these settings in your server configuration to create the perfect credit economy for your community.

## Support

For questions, bug reports, feature requests, or general support, please visit the Credify GitHub repository.

**Enjoy Credify and enhance your IW4MAdmin server with an exciting credit-based economy!**
