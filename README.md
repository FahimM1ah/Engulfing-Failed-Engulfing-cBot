# Engulfing and Failed Engulfing Strategy

This cAlgo trading bot identifies bullish and bearish engulfing and failed engulfing patterns. It allows for configurable risk management and trading settings, including an option for confluence with higher time frames (4-hour charts).

## Parameters

### Times
- **Start Time**: The start time for the bot to begin looking for trade opportunities.
- **End Time**: The end time for the bot to stop looking for trade opportunities.

### Trade Settings
- **Risk Type**: Determines the risk management strategy, either fixed lot size or percentage risk based on the stop loss size.
- **Volume for fixed lot**: The volume to trade if using a fixed lot size.
- **% Risk**: The percentage of the account balance to risk per trade if using percentage risk.
- **Take Profit Method**: The method to use for take profit, either based on a risk-reward ratio or a fixed number of pips.
- **Risk Reward Ratio**: The risk-reward ratio to use if the take profit method is set to risk-reward.
- **Take Profit Pips**: The number of pips for the take profit if the take profit method is set to fixed pips.
- **Stop Loss Method**: The method to use for stop loss, either based on the entry candle or a fixed number of pips.
- **Stop Loss Pips**: The number of pips for the stop loss if the stop loss method is set to fixed pips.
- **Use 4h Confluence**: Whether to use a higher time frame (4-hour chart) for additional confluence.

## Core Logic

### Initialization

The `OnStart` method initializes the 15-minute and 4-hour bars and converts the provided start and end times to `TimeOnly` objects.

### OnTick

The `OnTick` method is called on every tick and checks if the current time is within the trading window. If it is, it calls methods to enter buy or sell trades based on identified patterns.

### OnBar

The `OnBar` method is called on the formation of a new bar. It clears previous data, re-identifies engulfing patterns, and finds potential trade opportunities by checking for failed engulfing patterns and 4-hour confluence if enabled.

## Engulfing Patterns

### Bullish Engulfing

- **IsBear**: Checks if a given bar is bearish.
- **IsNextCandleBull**: Checks if the next candle after a given bearish candle is bullish.
- **IsBullishEngulfing**: Checks if a series of candles form a bullish engulfing pattern.
- **BullishEngChecks**: Combines the above methods to identify a bullish engulfing pattern.
- **DoesBullishEngulfingFail**: Checks if a bullish engulfing pattern is invalidated by subsequent bearish price action.
- **BullishEngulfings**: Scans bars to find bullish engulfing patterns that have not failed.

### Bearish Engulfing

- **IsBull**: Checks if a given bar is bullish.
- **IsNextCandleBear**: Checks if the next candle after a given bullish candle is bearish.
- **IsBearishEngulfing**: Checks if a series of candles form a bearish engulfing pattern.
- **BearishEngChecks**: Combines the above methods to identify a bearish engulfing pattern.
- **DoesBearishEngulfingFail**: Checks if a bearish engulfing pattern is invalidated by subsequent bullish price action.
- **BearishEngulfings**: Scans bars to find bearish engulfing patterns that have not failed.

## Combination and Entry Logic

### Bullish Combinations

- **BullishEngWithEngFailFinder**: Finds bearish engulfing patterns that fail after a bullish engulfing pattern and adds them to the list of valid trade opportunities.
- **IsBullLowerThanBear**: Checks if a bullish engulfing bar's low is lower than the failed bearish engulfing bar's low.
- **EnterBuys**: Enters buy trades if the current price matches the high of a recent valid bullish combination pattern.

### Bearish Combinations

- **BearishEngWithEngFailFinder**: Finds bullish engulfing patterns that fail after a bearish engulfing pattern and adds them to the list of valid trade opportunities.
- **IsBearHigherThanBull**: Checks if a bearish engulfing bar's high is higher than the failed bullish engulfing bar's high.
- **EnterSells**: Enters sell trades if the current price matches the low of a recent valid bearish combination pattern.

## 4-Hour Confluence

### Identification

- **FourHourBulls**: Identifies 4-hour bullish engulfing patterns.
- **FourHourBears**: Identifies 4-hour bearish engulfing patterns.

### Confluence Checks

- **IsBuySafe**: Checks if a buy entry is safe by ensuring the 15-minute pattern is supported by a 4-hour bullish pattern.
- **IsSellSafe**: Checks if a sell entry is safe by ensuring the 15-minute pattern is supported by a 4-hour bearish pattern.

## Risk and Position Management

### Risk Calculation

- **CalculateStopLoss**: Calculates the stop loss based on the selected strategy.
- **CalculateTP**: Calculates the take profit based on the selected strategy.
- **CalculateVolume**: Calculates the trade volume based on the selected risk strategy.

### Position Management

- **OpenPosition**: Opens a market order with the calculated stop loss, take profit, and volume.


## Disclaimer
Trading in financial markets involves risk, and there is a possibility of losing capital. This bot should be used as a tool to assist in trading decisions and not as a guaranteed method for profit. Users are responsible for configuring and testing the bot to ensure it aligns with their trading strategy and risk tolerance. Past performance is not indicative of future results when backwards testing over historical data.
