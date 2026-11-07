# Nav row hover lift

Work item 49, *Add hover animations when exploring the nav bar.*

## What was built

- A nav row's icon and label rise 1.5 DIP as the pointer passes over the row, over a 120 ms `CubicEaseOut` tween, and settle back when it leaves.
- One style in `NavigationView.axaml`: `Button.nav:pointerover StackPanel.navContent` sets `RenderTransform` to `translateY(-1.5px)`, and the content carries a `TransformOperationsTransition`.
- Reduce motion turns off both the tween and the lift, from a rule later in the same file.

## Decisions

- **A render transform on the content only.** Layout never changes, so no neighboring row shifts, and the row's hover background and accent indicator stay where they are.
- **Time-based tween.** 120 ms at every interface and text size, so it runs at the same speed at every scale. The 1.5 DIP distance sits inside the `ScaleHost`, so it grows with the interface size like everything else.
- **The selected row lifts too.** It is still a button, and every row answering the pointer alike reads as one control.
- **Pointer only.** Keyboard focus already draws Fluent's focus ring, and a lift on focus would fight it.
- **The Reduce motion rule sits after the hover rule in the same file.** A local style outranks an app-level one, and the later of two matching rules wins, so the undo cannot live in `SharedStyles.axaml`. An instant 1.5 DIP jump is still motion, so the lift goes too, not only the tween.

## How to verify

1. `dotnet test`: `NavigationViewMotionTests` pins the direction and size, the tween, and that the Reduce motion rule follows the hover rule and clears both.
2. In the app, move the pointer down the nav bar: each icon and label lifts slightly and settles back, and the rows above and below do not move.
3. Turn on Settings → Accessibility → Reduce motion: rows no longer lift.
4. At 200 % interface size, the lift takes the same time.
