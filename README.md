# Voltorb Flip Solver

An autonomous Voltorb Flip game solver that reads screen info to get the current state of the game and calculates the best moves accordingly.

# Application Layout

![Application Example Image](/example.jpg "Example")

## Controls

At the bottom of the screen are several different buttons to control the program's behavior
- **Continuous Capture Mode** - if enabled, the application will continuously capture screen information and automatically update the board accordingly
- **Calibrate** - when clicked, the application will scan the screen, looking for an open game of voltorb flip and store its positional information for later use (top-left square on the board must not be flipped yet for proper calibration)
- **Capture Screen** - if Continuous Capture Mode is disabled, use this button to capture screen information
- **Current Level** - use the dropdown to select which level of the game you are currently on, or use the arrow button to increment it

## Board

The onscreen board will show which cards have already been flipped as well as all possible values for unflipped cards. Cards that cannot possibly hold a voltorb will be surrounded by a green border.

# Game Strategy

This application uses a number of general rules to calculate the possible values at each location ([basic rules](https://www.dragonflycave.com/johto/voltorb-flip))
1. If a line has 0 voltorbs, remove ***voltorb*** as a possibility
2. If a line has 5 voltorbs, leave ***voltorb*** as the only possibility
3. If a line has 4 voltorbs, the only possibilities will be ***voltorb*** or the line's ***point total***
4. If the number of voltorbs + the line's remaining point total = the number of squares left, the only possibilities are ***1*** or ***voltorb*** (Ex: a row with 1 voltorb and a point total of 4 can only have ***1s*** or ***voltorbs***)
5. If the number of voltorbs + the line's remaining point total = the number of squares left + 1, there can be ***voltorbs***, ***1s***, or ***2s***, but no ***3s***
6. If the line's remaining point total >= 2 + (the number of squares remaining - 1) * 3, the point total is too high for ***1s*** to be a possibility
7. This rule has 3 parts to it. Let p = the total number of points left in a line, f = the number of free squares remaining (i.e. the number of squares left - the number of voltorbs)
    - If p < 2f, there are at minimum 2f - p ***1s***
    - If p > 2f, there are at minimum p - 2f ***3s***
    - If the parity of p is different from the parity of f (p % 2 != f % 2), there is a single ***2*** in the line
8. After calculating these more basic rules, the application then analyzes the possible combinations of point values that sum to the needed total value to figure out which squares MUST hold points in order to achieve the desired sum (i.e. a line with a total of 6 could be either 1, 1, 2, 2 or 1, 1, 1, 3). Using an example from [DragonflyCave](https://www.dragonflycave.com/johto/voltorb-flip),
![Rule 8 Example](/rule8-example.gif)<br>
there must be either 2 twos or 1 three, and in either case, the 4th card must be used as a ***2*** or a ***3***. Therefore, it cannot possibly be a ***voltorb***.
9. If the number of potential ***2s*** or ***3s*** on the board <= the total number of ***2s*** or ***3s*** for [each possible board in the given level](https://bulbapedia.bulbagarden.net/wiki/Voltorb_Flip#:~:text=contain%20more%20Voltorbs%3A-,Level,-%C3%972s), they are all guaranteed to be ***2s*** or ***3s***. This is useful during the endgame in which all other rules have already exhausted their use.

Throughout the evaluation of these rules, the application constantly keeps track of which of the [predetermine boards for the level](https://bulbapedia.bulbagarden.net/wiki/Voltorb_Flip#:~:text=contain%20more%20Voltorbs%3A-,Level,-%C3%972s) are possible at any given point. It uses this informaiton to determine which combinations of point values are possible in each line, providing vital information in endgame states.