## MODIFIED Requirements

### Requirement: Tab Titles
The system SHALL title the live-monitoring management tab "Subscribe" and the message-composition
management tab "Publish", presenting them as a matched verb pair, and SHALL prefix each tab's
title with its 1-based position among the management tabs in `N:Title` format (no space around
the colon), reflecting the Alt+digit shortcut that switches to it.

#### Scenario: Monitoring tab is titled with its position number
- **WHEN** the management tab area is displayed and Subscribe is the first (leftmost) tab
- **THEN** the tab is titled "1:Subscribe"

#### Scenario: Composition tab is titled with its position number
- **WHEN** the management tab area is displayed and Publish is the second tab
- **THEN** the tab is titled "2:Publish"

### Requirement: Alt+Digit Tab Switching
The system SHALL allow the user to switch directly to a management tab from anywhere in the
application via a dedicated Alt+digit shortcut, where the digit is the tab's 1-based position
among the management tabs in left-to-right display order: Alt+1 for the first tab (Subscribe),
Alt+2 for the second tab (Publish).

#### Scenario: Alt+1 switches to Subscribe from anywhere
- **WHEN** the user presses Alt+1 while focus is anywhere in the application, including inside
  another tab's content
- **THEN** the Subscribe tab becomes the selected tab

#### Scenario: Alt+2 switches to Publish from anywhere
- **WHEN** the user presses Alt+2 while focus is anywhere in the application, including inside
  another tab's content
- **THEN** the Publish tab becomes the selected tab
