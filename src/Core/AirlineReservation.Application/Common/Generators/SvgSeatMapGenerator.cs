using System.Text;
using AirlineReservation.Application.DTOs.Seat;
using AirlineReservation.Domain.Enums;

namespace AirlineReservation.Application.Common.Generators;

/// <summary>
/// High-performance pure SVG generator that renders responsive, interactive aircraft cabin seat maps.
/// Supports both Narrow-body (Airbus A320/A321, Boeing 737) and Wide-body (Boeing 777-300ER) layouts.
/// Embeds CSS, interactive data attributes, SVG tooltips, and aircraft visual landmarks (wings, galley, WC).
/// </summary>
public static class SvgSeatMapGenerator
{
    public static string Generate(
        string flightNumber,
        string airlineName,
        string aircraftModel,
        IReadOnlyList<SeatRowDto> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        bool isWideBody = rows.Any(r => r.Seats.Any(s => s.ColumnLetter is "G" or "H" or "J" or "K"));
        int canvasWidth = isWideBody ? 620 : 460;
        int cabinLeft = isWideBody ? 60 : 50;
        int cabinRight = canvasWidth - cabinLeft;
        int cabinWidth = cabinRight - cabinLeft;

        // Calculate dynamic height based on row count
        int startY = 160; // Cockpit + Front Galley
        int currentY = startY;

        // Pre-compute row positions
        var rowPositions = new List<(SeatRowDto row, int y, int height)>();
        foreach (var row in rows)
        {
            int rowHeight = row.SeatClass == SeatClass.Business ? 44 : 36;
            int spacing = row.IsExitRow ? 22 : 10;
            currentY += spacing;
            rowPositions.Add((row, currentY, rowHeight));
            currentY += rowHeight;
        }

        int rearY = currentY + 20;
        int totalHeight = rearY + 120; // Rear Galley + Tail

        var sb = new StringBuilder(24576);

        // SVG Root
        sb.AppendLine($@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 {canvasWidth} {totalHeight}"" width=""100%"" height=""100%"" class=""seatmap-svg"" aria-label=""Seat map for flight {flightNumber}"">");

        // Embedded CSS Styles
        sb.AppendLine(@"  <defs>
    <linearGradient id=""fuselageGrad"" x1=""0%"" y1=""0%"" x2=""100%"" y2=""0%"">
      <stop offset=""0%"" stop-color=""#f1f5f9"" />
      <stop offset=""50%"" stop-color=""#ffffff"" />
      <stop offset=""100%"" stop-color=""#f1f5f9"" />
    </linearGradient>
    <linearGradient id=""businessGrad"" x1=""0%"" y1=""0%"" x2=""0%"" y2=""100%"">
      <stop offset=""0%"" stop-color=""#fef3c7"" />
      <stop offset=""100%"" stop-color=""#fde68a"" />
    </linearGradient>
    <linearGradient id=""economyGrad"" x1=""0%"" y1=""0%"" x2=""0%"" y2=""100%"">
      <stop offset=""0%"" stop-color=""#f0f9ff"" />
      <stop offset=""100%"" stop-color=""#e0f2fe"" />
    </linearGradient>
    <filter id=""seatShadow"" x=""-10%"" y=""-10%"" width=""120%"" height=""120%"">
      <feDropShadow dx=""0"" dy=""1"" stdDeviation=""1"" flood-opacity=""0.15"" />
    </filter>
  </defs>
  <style>
    .seatmap-svg { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; user-select: none; }
    .fuselage { fill: url(#fuselageGrad); stroke: #cbd5e1; stroke-width: 2.5; }
    .cockpit-window { fill: #0f172a; opacity: 0.85; }
    .wing { fill: #e2e8f0; stroke: #94a3b8; stroke-width: 2; }
    .row-label { font-size: 11px; font-weight: 600; fill: #64748b; text-anchor: middle; dominant-baseline: central; }
    .landmark-box { fill: #f8fafc; stroke: #e2e8f0; stroke-width: 1.5; stroke-dasharray: 4,3; rx: 6; }
    .landmark-text { font-size: 10px; font-weight: 700; fill: #94a3b8; text-anchor: middle; dominant-baseline: central; letter-spacing: 1px; }
    .seat { cursor: pointer; transition: transform 0.15s ease, filter 0.15s ease; }
    .seat:hover:not(.occupied) { transform: scale(1.08); filter: drop-shadow(0 4px 6px rgba(0,0,0,0.18)); }
    .seat-rect { rx: 6; ry: 6; stroke-width: 1.5; filter: url(#seatShadow); }
    .seat-headrest { rx: 2; ry: 2; fill-opacity: 0.6; }
    .seat-label { font-size: 11px; font-weight: 700; text-anchor: middle; dominant-baseline: central; }
    .seat.business .seat-rect { fill: url(#businessGrad); stroke: #d97706; }
    .seat.business .seat-headrest { fill: #b45309; }
    .seat.business .seat-label { fill: #78350f; }
    .seat.economy .seat-rect { fill: url(#economyGrad); stroke: #0284c7; }
    .seat.economy .seat-headrest { fill: #0369a1; }
    .seat.economy .seat-label { fill: #075985; }
    .seat.occupied { cursor: not-allowed; }
    .seat.occupied .seat-rect { fill: #e2e8f0; stroke: #94a3b8; }
    .seat.occupied .seat-headrest { fill: #64748b; }
    .seat.occupied .seat-label { fill: #94a3b8; }
    .seat.occupied line { stroke: #94a3b8; stroke-width: 1.5; }
    .exit-marker { fill: #ef4444; font-size: 10px; font-weight: 800; text-anchor: middle; }
    .legend-text { font-size: 11px; fill: #475569; font-weight: 500; }
  </style>");

        // Header / Legend Box
        sb.AppendLine($@"  <!-- Flight Header & Legend -->
  <g class=""header-legend"" transform=""translate(20, 15)"">
    <text x=""0"" y=""14"" font-size=""15"" font-weight=""800"" fill=""#0f172a"">{flightNumber} • {aircraftModel}</text>
    <text x=""0"" y=""32"" font-size=""11"" font-weight=""500"" fill=""#64748b"">{airlineName}</text>
    
    <g transform=""translate(0, 48)"">
      <!-- Business Legend -->
      <rect x=""0"" y=""0"" width=""14"" height=""14"" rx=""3"" fill=""#fde68a"" stroke=""#d97706"" stroke-width=""1.2"" />
      <text x=""20"" y=""11"" class=""legend-text"">Business</text>
      
      <!-- Economy Legend -->
      <rect x=""95"" y=""0"" width=""14"" height=""14"" rx=""3"" fill=""#e0f2fe"" stroke=""#0284c7"" stroke-width=""1.2"" />
      <text x=""115"" y=""11"" class=""legend-text"">Economy</text>
      
      <!-- Occupied Legend -->
      <rect x=""190"" y=""0"" width=""14"" height=""14"" rx=""3"" fill=""#e2e8f0"" stroke=""#94a3b8"" stroke-width=""1.2"" />
      <text x=""210"" y=""11"" class=""legend-text"">Occupied</text>
    </g>
  </g>");

        // Wings at Exit Rows
        var exitRowPositions = rowPositions.Where(r => r.row.IsExitRow).ToList();
        if (exitRowPositions.Count > 0)
        {
            int wingStartY = exitRowPositions.Min(r => r.y) - 30;
            int wingEndY = exitRowPositions.Max(r => r.y + r.height) + 30;
            int wingSpan = isWideBody ? 55 : 45;

            // Left Wing
            sb.AppendLine($@"  <!-- Left Wing -->
  <path d=""M {cabinLeft} {wingStartY} L {cabinLeft - wingSpan} {wingStartY + 40} L {cabinLeft - wingSpan} {wingEndY} L {cabinLeft} {wingEndY - 20} Z"" class=""wing"" />");
            // Right Wing
            sb.AppendLine($@"  <!-- Right Wing -->
  <path d=""M {cabinRight} {wingStartY} L {cabinRight + wingSpan} {wingStartY + 40} L {cabinRight + wingSpan} {wingEndY} L {cabinRight} {wingEndY - 20} Z"" class=""wing"" />");
        }

        // Fuselage Body
        int noseTop = 85;
        int noseRadius = cabinWidth / 2;
        int cabinCenterX = canvasWidth / 2;

        sb.AppendLine($@"  <!-- Fuselage Body -->
  <path d=""M {cabinLeft} {noseTop + noseRadius}
           A {noseRadius} {noseRadius * 1.3} 0 0 1 {cabinRight} {noseTop + noseRadius}
           L {cabinRight} {rearY + 40}
           Q {cabinCenterX} {totalHeight - 10} {cabinLeft} {rearY + 40}
           Z"" class=""fuselage"" />");

        // Cockpit Windows
        sb.AppendLine($@"  <!-- Cockpit Windows -->
  <path d=""M {cabinCenterX - 35} {noseTop + 25} Q {cabinCenterX - 15} {noseTop + 15} {cabinCenterX} {noseTop + 22} Q {cabinCenterX + 15} {noseTop + 15} {cabinCenterX + 35} {noseTop + 25} L {cabinCenterX + 28} {noseTop + 34} Q {cabinCenterX} {noseTop + 28} {cabinCenterX - 28} {noseTop + 34} Z"" class=""cockpit-window"" />");

        // Front Galley & Lavatory
        int frontLandmarkY = noseTop + noseRadius + 5;
        sb.AppendLine($@"  <!-- Front Galley / WC Landmark -->
  <rect x=""{cabinLeft + 20}"" y=""{frontLandmarkY}"" width=""{cabinWidth - 40}"" height=""26"" class=""landmark-box"" />
  <text x=""{cabinCenterX}"" y=""{frontLandmarkY + 13}"" class=""landmark-text"">FRONT GALLEY • LAVATORIES</text>");

        // Seat Columns Calculation & Rendering
        foreach (var (row, y, rowHeight) in rowPositions)
        {
            // Exit Row Label / Emergency Marker
            if (row.IsExitRow)
            {
                sb.AppendLine($@"  <!-- Exit Row Marker -->
  <text x=""{cabinLeft - 18}"" y=""{y + rowHeight / 2}"" class=""exit-marker"">EXIT</text>
  <text x=""{cabinRight + 18}"" y=""{y + rowHeight / 2}"" class=""exit-marker"">EXIT</text>");
            }

            // Row Number Center Indicator
            sb.AppendLine($@"  <!-- Row {row.RowNumber} -->
  <g class=""seat-row"" data-row=""{row.RowNumber}"">
    <text x=""{cabinCenterX}"" y=""{y + rowHeight / 2}"" class=""row-label"">{row.RowNumber}</text>");

            // Render Seats in this Row
            RenderSeatsInRow(sb, row, y, rowHeight, cabinLeft, cabinRight, cabinCenterX, isWideBody);

            sb.AppendLine("  </g>");
        }

        // Rear Galley / Lavatory
        sb.AppendLine($@"  <!-- Rear Galley / WC Landmark -->
  <rect x=""{cabinLeft + 20}"" y=""{rearY}"" width=""{cabinWidth - 40}"" height=""26"" class=""landmark-box"" />
  <text x=""{cabinCenterX}"" y=""{rearY + 13}"" class=""landmark-text"">REAR GALLEY • LAVATORIES</text>");

        // Close SVG
        sb.AppendLine("</svg>");

        return sb.ToString();
    }

    private static void RenderSeatsInRow(
        StringBuilder sb,
        SeatRowDto row,
        int y,
        int rowHeight,
        int cabinLeft,
        int cabinRight,
        int cabinCenterX,
        bool isWideBody)
    {
        bool isBusiness = row.SeatClass == SeatClass.Business;

        if (isBusiness)
        {
            // 2-2 Business Layout across all aircraft: A, C | aisle | D, F
            // Wider seats (36px width, 36px height)
            int seatWidth = 36;
            int seatH = 36;
            int gap = 8;
            int aisleWidth = 44;

            // Left Block: A, C
            int leftBlockStart = cabinCenterX - aisleWidth / 2 - (2 * seatWidth + gap);
            int curX = leftBlockStart;

            var seatA = row.Seats.FirstOrDefault(s => s.ColumnLetter == "A");
            var seatC = row.Seats.FirstOrDefault(s => s.ColumnLetter == "C");
            var seatD = row.Seats.FirstOrDefault(s => s.ColumnLetter == "D");
            var seatF = row.Seats.FirstOrDefault(s => s.ColumnLetter == "F");

            if (seatA != null) RenderSingleSeat(sb, seatA, curX, y, seatWidth, seatH);
            curX += seatWidth + gap;
            if (seatC != null) RenderSingleSeat(sb, seatC, curX, y, seatWidth, seatH);

            // Right Block: D, F
            curX = cabinCenterX + aisleWidth / 2;
            if (seatD != null) RenderSingleSeat(sb, seatD, curX, y, seatWidth, seatH);
            curX += seatWidth + gap;
            if (seatF != null) RenderSingleSeat(sb, seatF, curX, y, seatWidth, seatH);
        }
        else if (!isWideBody)
        {
            // Narrow-body Economy (3-3): A, B, C | aisle | D, E, F
            int seatWidth = 30;
            int seatH = 30;
            int gap = 6;
            int aisleWidth = 36;

            // Left: A, B, C
            int leftStart = cabinCenterX - aisleWidth / 2 - (3 * seatWidth + 2 * gap);
            int curX = leftStart;

            foreach (var col in new[] { "A", "B", "C" })
            {
                var seat = row.Seats.FirstOrDefault(s => s.ColumnLetter == col);
                if (seat != null) RenderSingleSeat(sb, seat, curX, y, seatWidth, seatH);
                curX += seatWidth + gap;
            }

            // Right: D, E, F
            curX = cabinCenterX + aisleWidth / 2;
            foreach (var col in new[] { "D", "E", "F" })
            {
                var seat = row.Seats.FirstOrDefault(s => s.ColumnLetter == col);
                if (seat != null) RenderSingleSeat(sb, seat, curX, y, seatWidth, seatH);
                curX += seatWidth + gap;
            }
        }
        else
        {
            // Wide-body Boeing 777-300ER Economy (3-4-3):
            // A, B, C | Aisle 1 | D, E, F, G | Aisle 2 | H, J, K (strictly no 'I')
            int seatWidth = 28;
            int seatH = 28;
            int gap = 4;
            int aisleWidth = 24;

            int centerBlockWidth = 4 * seatWidth + 3 * gap;
            int centerStart = cabinCenterX - centerBlockWidth / 2;

            int leftStart = centerStart - aisleWidth - (3 * seatWidth + 2 * gap);
            int rightStart = centerStart + centerBlockWidth + aisleWidth;

            // Left block (A, B, C)
            int curX = leftStart;
            foreach (var col in new[] { "A", "B", "C" })
            {
                var seat = row.Seats.FirstOrDefault(s => s.ColumnLetter == col);
                if (seat != null) RenderSingleSeat(sb, seat, curX, y, seatWidth, seatH);
                curX += seatWidth + gap;
            }

            // Center block (D, E, F, G)
            curX = centerStart;
            foreach (var col in new[] { "D", "E", "F", "G" })
            {
                var seat = row.Seats.FirstOrDefault(s => s.ColumnLetter == col);
                if (seat != null) RenderSingleSeat(sb, seat, curX, y, seatWidth, seatH);
                curX += seatWidth + gap;
            }

            // Right block (H, J, K)
            curX = rightStart;
            foreach (var col in new[] { "H", "J", "K" })
            {
                var seat = row.Seats.FirstOrDefault(s => s.ColumnLetter == col);
                if (seat != null) RenderSingleSeat(sb, seat, curX, y, seatWidth, seatH);
                curX += seatWidth + gap;
            }
        }
    }

    private static void RenderSingleSeat(
        StringBuilder sb,
        SeatDto seat,
        int x,
        int y,
        int width,
        int height)
    {
        bool isUnavailable = seat.Status != SeatStatus.Available;
        string statusClass = isUnavailable ? seat.Status.ToString().ToLowerInvariant() : "available";
        string classModifier = seat.SeatClass == SeatClass.Business ? "business" : "economy";
        string ariaDisabled = isUnavailable ? @"aria-disabled=""true""" : @"role=""button"" tabindex=""0""";

        sb.Append($@"    <g class=""seat {classModifier} {statusClass}"" ")
          .Append($@"id=""seat-{seat.Id}"" ")
          .Append($@"data-seat-id=""{seat.Id}"" ")
          .Append($@"data-seat-number=""{seat.SeatNumber}"" ")
          .Append($@"data-row=""{seat.RowNumber}"" ")
          .Append($@"data-column=""{seat.ColumnLetter}"" ")
          .Append($@"data-class=""{seat.SeatClass}"" ")
          .Append($@"data-price=""{seat.Price:F2}"" ")
          .Append($@"data-status=""{statusClass}"" ")
          .Append($@"{ariaDisabled}>");

        // Native SVG Accessible Tooltip
        string tooltipText = isUnavailable
            ? $"Seat {seat.SeatNumber} - {seat.Status}"
            : $"Seat {seat.SeatNumber} ({seat.SeatClass}) - {seat.Price:F2} AZN";
        sb.Append($@"<title>{tooltipText}</title>");

        // Seat Main Body
        sb.Append($@"<rect x=""{x}"" y=""{y}"" width=""{width}"" height=""{height}"" class=""seat-rect"" />");

        // Seat Headrest
        int headrestHeight = (int)(height * 0.28);
        sb.Append($@"<rect x=""{x + 3}"" y=""{y + 3}"" width=""{width - 6}"" height=""{headrestHeight}"" class=""seat-headrest"" />");

        if (isUnavailable)
        {
            // Occupied Diagonal Lock Strike
            sb.Append($@"<line x1=""{x + 5}"" y1=""{y + 5}"" x2=""{x + width - 5}"" y2=""{y + height - 5}"" />");
        }
        else
        {
            // Column Letter Label
            sb.Append($@"<text x=""{x + width / 2}"" y=""{y + height / 2 + 3}"" class=""seat-label"">{seat.ColumnLetter}</text>");
        }

        sb.AppendLine("</g>");
    }
}
