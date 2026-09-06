# dropdown-popup-width Specification

## Purpose

A `DropDownList<T>`'s expanded popup sizes itself to the anchoring control's own width rather
than to its longest item's text, so the popup reads as an extension of the closed control instead
of an independently-sized overlay.

## Requirements

### Requirement: Popup width matches the anchor control
A `DropDownList<T>`'s expanded popup SHALL size its width to match the width of the
`DropDownList<T>` control that anchors it, rather than to the width of its longest item's text.

#### Scenario: Popup width equals the closed control's width
- **WHEN** a `DropDownList<T>` (e.g. Ack Policy / Deliver Policy in `CreateConsumerDialog`,
  Retention in `CreateStreamDialog`) is expanded
- **THEN** the popup's rendered width equals the width of the closed `DropDownList<T>` control,
  regardless of how long its longest item's text is

#### Scenario: A long item is clipped rather than widening the popup
- **WHEN** a `DropDownList<T>`'s source contains an item whose text is longer than the control's
  own width
- **THEN** the expanded popup still renders at the control's width, and that item's text is
  clipped to fit rather than the popup widening to accommodate it

#### Scenario: Popup width tracks the control across re-opens
- **WHEN** a `DropDownList<T>` control's own width changes (e.g. a different dialog uses a
  differently-sized `EditFrame`) and its popup is subsequently opened
- **THEN** the popup's width reflects the control's current width at the time it is opened, not a
  value cached from an earlier open
