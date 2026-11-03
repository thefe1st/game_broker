using System.Text;
using Broker.Core;
using UnityEngine;

namespace Broker.Runtime.Office
{
    // The Exchange north of the boulevard: the podium with its steps and the bull and bear on the cheeks, the hall with
    // the quote board, the roof with «БИРЖА» on the frieze (the colonnade comes from CityScenery) and the colliders of
    // the podium edge and the hall walls. The board shows ExchangeBoard for the current day and changes nothing.
    public sealed partial class OfficeScene
    {
        private TextMesh boardTitle, boardTape;
        private readonly TextMesh[,] boardCells = new TextMesh[6, 4];
        private BrokerDay boardDay;
        private int boardDayNumber = -1;
        private bool boardQuotes;
        private static readonly Color BoardGold = new Color(0.96f, 0.82f, 0.56f), BoardCream = new Color(0.93f, 0.9f, 0.82f),
            BoardUp = new Color(0.45f, 0.95f, 0.6f), BoardDown = new Color(1f, 0.45f, 0.4f), BoardFlat = new Color(0.72f, 0.76f, 0.82f);

        private void BuildExchange()
        {
            const float g = CityBlockPlan.GroundY, floor = g + CityDistrictPlan.PodiumFloorY;
            const float stepsZ = CityDistrictPlan.StepsMinZ, podiumZ = CityDistrictPlan.PodiumMinZ, backZ = CityDistrictPlan.PodiumMaxZ;
            const float podiumHalf = CityDistrictPlan.PodiumHalfWidth, stepsHalf = CityDistrictPlan.StepsHalfWidth;
            PlaceVoxel("exchange podium", VoxelProps.ExchangePodium, "Exchange / voxel podium", transform, new Vector3(0, CityBlockPlan.TerrainTopY, stepsZ));
            PlaceVoxel("exchange hall", VoxelProps.ExchangeHall, "Exchange / voxel hall", transform, new Vector3(0, floor, 60));
            PlaceVoxel("exchange roof", VoxelProps.ExchangeRoof, "Exchange / voxel roof", transform, new Vector3(0, floor + 7, CityScenery.ExchangeRoofZ));
            Text("Exchange / frieze", "Б И Р Ж А", new Vector3(0, floor + 7.75f, CityScenery.ExchangeRoofZ + 0.2f), 64, 0.08f, new Color(0.15f, 0.23f, 0.34f));
            // Bull and bear face each other across the steps.
            PlaceVoxel("exchange statue/bull", () => VoxelProps.ExchangeStatue(true), "Exchange / bull", transform, new Vector3(-10.8f, floor, 55.8f), -90);
            PlaceVoxel("exchange statue/bear", () => VoxelProps.ExchangeStatue(false), "Exchange / bear", transform, new Vector3(10.8f, floor, 55.8f), 90);
            // Podium top, then the steps as one smooth ramp for the controller: from the square (+0.12 m) to +1.1 m over 2 m.
            Blocker("Exchange / podium floor", new Vector3(0, floor - 0.25f, (podiumZ + backZ) / 2), new Vector3(2 * podiumHalf, 0.5f, backZ - podiumZ));
            const float rise = CityDistrictPlan.PodiumFloorY - CityDistrictPlan.NorthFloorY, run = podiumZ - stepsZ;
            float slope = (float)(System.Math.Atan2(rise, run) * 180 / System.Math.PI);
            var tilt = Quaternion.Euler(-slope, 0, 0);
            var middle = new Vector3(0, g + (CityDistrictPlan.NorthFloorY + CityDistrictPlan.PodiumFloorY) / 2, (stepsZ + podiumZ) / 2) - tilt * Vector3.up * 0.1f;
            var ramp = Blocker("Exchange / steps ramp", middle, new Vector3(2 * stepsHalf, 0.2f, (float)System.Math.Sqrt(rise * rise + run * run) + 0.1f));
            ramp.transform.localRotation = tilt;
            // Cheeks with the statues, the parapet along the sides and the back, the walls of the hall around its door.
            foreach (float side in new[] { -1f, 1f })
            {
                Blocker("Exchange / step cheek", new Vector3(side * (stepsHalf + podiumHalf) / 2, g + 2, (stepsZ + podiumZ) / 2), new Vector3(podiumHalf - stepsHalf, 4, run));
                Blocker("Exchange / podium edge", new Vector3(side * (podiumHalf + 0.05f), floor + 0.75f, (podiumZ + backZ) / 2), new Vector3(0.5f, 1.5f, backZ - podiumZ));
                Blocker("Exchange / hall side wall", new Vector3(side * 8.75f, floor + 3.5f, 66), new Vector3(0.5f, 7, 12));
                Blocker("Exchange / hall front wall", new Vector3(side * 5.25f, floor + 3.5f, 60.25f), new Vector3(7.5f, 7, 0.5f));
            }
            Blocker("Exchange / podium back", new Vector3(0, floor + 0.75f, backZ + 0.05f), new Vector3(2 * podiumHalf + 0.6f, 1.5f, 0.5f));
            Blocker("Exchange / hall back wall", new Vector3(0, floor + 3.5f, 71.75f), new Vector3(18, 7, 0.5f));
            Blocker("Exchange / hall lintel", new Vector3(0, floor + 5.5f, 60.25f), new Vector3(3, 3, 0.5f));
            BuildQuoteBoard(floor);
            foreach (float z in new[] { 63.5f, 68.5f }) PointLight("Exchange / hall light", new Vector3(0, floor + 5.6f, z), 12, 0.9f, new Color(1, 0.88f, 0.7f));
        }
        // The board hangs on the back wall facing the door: a title row, six rows of ticker, name, price and change,
        // and a tape along the bottom edge.
        private void BuildQuoteBoard(float floor)
        {
            float bottom = floor + 1.6f;
            const float face = 71.27f;
            PlaceVoxel("quote board", VoxelProps.QuoteBoard, "Exchange / voxel quote board", transform, new Vector3(0, bottom, 71.4f));
            var board = Blocker("Exchange / quote board", new Vector3(0, floor + 2.75f, 71.3f), new Vector3(8.4f, 4.5f, 0.4f));
            Interactable(board, OfficeStation.CityExchange, "Табло котировок", "Посмотреть цены дня");
            boardTitle = Text("Exchange / board title", ExchangeBoard.ClosedTitle, new Vector3(0, bottom + 3.0f, face), 48, 0.055f, BoardGold);
            float[] columns = { -3.4f, -1.25f, 1.35f, 3.25f };
            for (int row = 0; row < 6; row++)
                for (int column = 0; column < 4; column++)
                    boardCells[row, column] = Text("Exchange / board cell", "", new Vector3(columns[column], bottom + 2.6f - row * 0.4f, face), 48,
                        column == 1 ? 0.042f : 0.055f, column == 0 ? BoardGold : column == 1 ? BoardCream : Color.white);
            boardTape = Text("Exchange / board tape", "", new Vector3(0, bottom + 0.2f, face), 48, 0.03f, WaitAmber);
        }
        // Called every frame from Refresh; the texts change only with the day.
        private void RefreshBoard(BrokerDay day)
        {
            if (boardTitle == null || (day == boardDay && day.DayNumber == boardDayNumber && day.HasQuotes == boardQuotes)) return;
            boardDay = day; boardDayNumber = day.DayNumber; boardQuotes = day.HasQuotes;
            var rows = ExchangeBoard.Rows(day);
            boardTitle.text = ExchangeBoard.Title(day);
            var tape = new StringBuilder();
            for (int row = 0; row < 6; row++)
            {
                var quote = row < rows.Count ? rows[row] : null;
                boardCells[row, 0].text = quote == null ? "" : quote.Ticker;
                boardCells[row, 1].text = quote == null ? "" : quote.Name;
                boardCells[row, 2].text = quote == null ? "" : quote.Price;
                boardCells[row, 3].text = quote == null ? "" : quote.Change;
                boardCells[row, 3].color = quote == null || quote.Direction == 0 ? BoardFlat : quote.Direction > 0 ? BoardUp : BoardDown;
                if (quote != null) tape.Append(tape.Length > 0 ? "   ·   " : "").Append(quote.Ticker).Append(' ').Append(quote.Change);
            }
            boardTape.text = rows.Count > 0 ? tape.ToString() : "КОТИРОВКИ — В РАБОЧИЕ ДНИ ПО НАЙМУ И В СВОЕЙ КОНТОРЕ";
        }
    }
}
