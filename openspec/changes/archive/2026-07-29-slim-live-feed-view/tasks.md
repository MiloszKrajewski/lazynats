## 1. Slim down LiveUpdatesView

- [x] 1.1 Remove the `divider` (`Line`) and `heading` (`Label`) fields and their construction from
      `LiveUpdatesView`'s constructor.
- [x] 1.2 Move `_listView`'s `Y` from `2` to `0`, and drop `divider`/`heading` from the `Add(...)`
      call.
- [x] 1.3 Run the app against a NATS server and visually confirm the "Live Feed" frame shows only
      the frame's own title, with the message list filling the pane immediately below the border.
