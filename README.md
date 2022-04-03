# CreditPlugin
Plugin for IW4MAdmin - tested with MW2 and CS:GO

Betting system that allows people to gamble their hard earned credits by betting on a Team or player's outcome. 

They can also mindlessly gamble the credits in a 0-10 RNG battle.

# Commands
## User Commands
  !credits \<optional name> (!cr) - Returns yours or someone's credits.

  !topcredits (!tcr) - List top players by credits.
    
  !gamble \<0-10> \<amount> (!gmb) - Gambles your credits, 1 in 10 chance, return is double your money.
  
  !betplayer \<name> \<amount> (!betp) - Bet on a player to win - Payout is proportionate based on their ELO.
  
  !betteam \<name> \<amount> (!bett) - Bet on a team to win - Payout is proportionate based team's average ELO.
  
  !claimbets (!cb) - Claim any expired/completed bets you've previously made. (No credits lost if unclaimed)
  
  !openbets (!ob) - Lists current bets on your server. (ID - Origin - Target - Amount)
  
  !cancelbets (!cnclb) - Cancels your existing, open bets.
  
  !creditstats (!statscr) - Lists the current credits statistics, ie: how many have been earned, spent and how has been won.

  
  ##### Small note, you can use the keyword "all" in place of the amount. Of course, you will be staking everything you have.

## Admin Commands
!setcredits \<name> \<amount> (!scr) - Set a player's credits.
