# Drag animation for reorderable widgets

Work item 14, *Add drag animation for dashboard widgets.* Its predecessors, work items 17 and 18 (the drag flicker and glitch fixes), are closed.

## What was built

- **Neighbours slide.** While a drag re-packs the board, every widget whose slot moved eases from where it was drawn to its new slot instead of jumping there.
- **The drop settles.** On release, the dropped widget eases from the box it was let go at into its slot, drawn above its neighbours until it arrives.
- **One motion model.** `ReorderSlides` (`src/Shared/Layout`) holds the per-item offsets: ease-out cubic over 180 ms. A widget re-targeted mid-slide continues from where it is drawn. It has no Avalonia types, so it is unit-tested directly.
- **Applied in `ReorderablePanel.Placed`**, the one arrange path both reorderable panels share. Every board and strip gets the motion: Dashboard, Network and Storage widgets, Storage drive cards, Hardware cards, the Processes summary tiles, and the Performance rail and stat tiles. Frames come from `TopLevel.RequestAnimationFrame` and stop once nothing is moving.

## Decisions

- **A render transform, not layout.** The offset is a `TranslateTransform` on the moving child. Measure, arrange, the frozen drop slots (`DropTarget`) and the drop hint never see it, so the flicker fix and the pixel snap are untouched, and a frame redraws without a layout pass.
- **Only a drag animates.** A keyboard move (`Ctrl+Shift+←/→`), a window resize and a saved-order reset still land at once. The work item asks for drag motion, and animating a resize would lag the layout behind the window edge.
- **"Rejected" means a drag that never dropped.** The drag model has no drop that is refused: a release always commits, even back onto the widget's own slot, and that case still eases in. A drag that ends without a release, through lost pointer capture, commits nothing and puts every widget straight back with no motion.
- **Reduce motion turns all of it off.** It is read in code from the window's `reduceMotion` class, the same signal the style-driven transitions use, because these slides are not style transitions.
- **Position only.** A preview that changes a slot's width (unequal widgets) still resizes at once. Only the position slides.

## How to verify

1. `dotnet test`: `ReorderSlidesTests` covers the easing and the mid-slide hand-off. `ReorderablePanelTests` covers neighbours sliding during a drag, the dropped widget easing in from its let-go box, a cancel, a keyboard move, a resize and Reduce motion leaving no transform, and every slide clearing after the duration.
2. In the app, drag a Dashboard widget across its neighbours: they glide aside rather than jumping. Let go and the widget glides into its slot.
3. Turn on Settings → Accessibility → Reduce motion and repeat: everything lands at once.
4. Press `Ctrl+Shift+→` on a focused widget: it moves at once, with no slide.
