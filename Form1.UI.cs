using System;
using System.Drawing;
using My2DEngine.Engine.Rendering;
using My2DEngine.Game.Rendering.Ui;
namespace My2DEngine
{
    /// <summary>
    /// Form1의 UI 렌더링 partial 클래스.
    /// 메인 메뉴, 모드 선택, 설정, 기록, 일시정지/사망 오버레이의 레이아웃 계산과 드로우를 담당한다.
    /// </summary>
    public partial class Form1
    {
        /// <summary>UI 레이아웃 기준 해상도 너비. 모든 메뉴/오버레이 좌표는 이 크기에서 1.0배가 되도록 설계한다.</summary>
        private const float UiBaseWidth = 1280f;

        /// <summary>UI 레이아웃 기준 해상도 높이.</summary>
        private const float UiBaseHeight = 720f;

        /// <summary>
        /// 현재 클라이언트 크기에 대한 균일 UI 배율을 반환한다.
        /// 기준 해상도(1280x720) 대비 가로/세로 중 작은 비율을 사용해 종횡비가 달라도 왜곡 없이
        /// 비례하며, 극단적으로 작거나 큰 창에서도 가독성을 유지하도록 클램프한다.
        /// 메뉴/설정/기록/사망/일시정지 화면의 폰트·패널·버튼 크기에 곱해 사용한다.
        /// </summary>
        private float GetUiScale(int width, int height)
        {
            float s = Math.Min(width / UiBaseWidth, height / UiBaseHeight);
            if (s < 0.55f) s = 0.55f;
            if (s > 1.8f) s = 1.8f;
            return s;
        }

        /// <summary>
        /// 창 크기가 변경된 경우에만 모든 UI 레이아웃을 재계산한다.
        /// 이전과 동일한 크기이면 캐시된 값을 재사용하여 불필요한 계산을 생략한다.
        /// </summary>
        /// <param name="width">현재 클라이언트 영역 너비.</param>
        /// <param name="height">현재 클라이언트 영역 높이.</param>
        private void EnsureUiLayout(int width, int height)
        {
            if (width == cachedLayoutWidth && height == cachedLayoutHeight)
            {
                return;
            }

            cachedLayoutWidth = width;
            cachedLayoutHeight = height;
            UpdateMenuLayout(width, height);
            UpdateModeLayout(width, height);
            UpdatePauseLayout(width, height);
            UpdateDeathLayout(width, height);
            UpdateSettingsLayout(width, height);
        }

        /// <summary>메뉴 배경 애니메이션 시간(초).</summary>
        private static float MenuTime => Environment.TickCount64 / 1000f;

        /// <summary>
        /// 메인 메뉴 화면을 그린다. 절차적 배경, 픽셀 타이틀, 부제, 네 버튼, 영구 스탯 안내를 그린다.
        /// 마우스 커서 위치를 기반으로 버튼 hover 효과를 적용한다.
        /// </summary>
        /// <param name="r">드로우 명령을 받을 렌더러 인스턴스.</param>
        /// <param name="width">클라이언트 영역 너비 (레이아웃 기준).</param>
        /// <param name="height">클라이언트 영역 높이 (레이아웃 기준).</param>
        private void DrawMenu(Renderer r, int width, int height)
        {
            DrawSharedMenuBackground(r, width, height);

            float s = GetUiScale(width, height);
            float u = PixelUi.UnitFor(s);
            float cx = width * 0.5f;

            // 타이틀: 굵은 외곽선 글자. 아주 약하게 깜박여 불빛 느낌을 준다.
            float titleY = height * 0.22f;
            float flicker = (float)(Math.Sin(MenuTime * 7.3) * Math.Sin(MenuTime * 3.1));
            Color titleColor = flicker > 0.93f ? PixelPalette.AccentHot : PixelPalette.Accent;
            PixelUi.Title(r, "KILLING FIELD", cx, titleY, titleColor, PixelUi.FontBase * u * 3f, u);

            float subY = titleY + PixelUi.FontBase * u * 3f;
            float subSize = PixelUi.FontBase * u;
            PixelUi.TextCentered(r, "ABANDONED SECTOR SURVIVAL", cx, subY, PixelPalette.TextDim, subSize, u);
            float lineW = 64f * u;
            float lineGap = r.MeasureText("ABANDONED SECTOR SURVIVAL", subSize).Width * 0.5f + u * 8f;
            r.DrawRectangle(cx - lineGap - lineW, subY, lineW, u, PixelPalette.Blood);
            r.DrawRectangle(cx + lineGap, subY, lineW, u, PixelPalette.Blood);

            DrawMenuActionButton(r, startButtonRect, "게임 시작", "모드 선택 후 전장 진입", PixelPalette.Accent, startButtonRect.Contains(lastMousePosition));
            DrawMenuActionButton(r, settingsButtonRect, "설정", "시야각·감도·소리·창 크기", PixelPalette.Brass, settingsButtonRect.Contains(lastMousePosition));
            DrawMenuActionButton(r, recordsButtonRect, "기록", "역대 도전 기록 보기", PixelPalette.Info, recordsButtonRect.Contains(lastMousePosition));
            DrawMenuActionButton(r, exitButtonRect, "게임 종료", "세션 종료", PixelPalette.TextDim, exitButtonRect.Contains(lastMousePosition));

            float hintSize = PixelUi.FontBase * u * 0.5f;
            PixelUi.Text(r, "[I] 영구 스탯 보기", 12f * u, height - 12f * u - hintSize, PixelPalette.TextDim, hintSize, u);
        }

        /// <summary>
        /// 모드 선택 화면을 그린다. 패널, 설명, 일반 모드/무한 모드/이어하기/뒤로 버튼.
        /// </summary>
        /// <param name="r">드로우 명령을 받을 렌더러 인스턴스.</param>
        /// <param name="width">클라이언트 영역 너비.</param>
        /// <param name="height">클라이언트 영역 높이.</param>
        private void DrawModeSelect(Renderer r, int width, int height)
        {
            DrawSharedMenuBackground(r, width, height);

            float s = GetUiScale(width, height);
            float u = PixelUi.UnitFor(s);
            int panelW = (int)(600 * s);
            int panelH = GetModePanelHeight(s);
            int panelX = (width - panelW) / 2;
            int panelY = (height - panelH) / 2;

            float contentY = PixelUi.Panel(r, panelX, panelY, panelW, panelH, u, "진행 모드 선택");
            PixelUi.TextCentered(r, "전투 밸런스는 기본값으로 고정됩니다", width * 0.5f, contentY + 8f * u, PixelPalette.TextDim, PixelUi.FontBase * u * 0.5f, u);

            DrawMenuActionButton(r, modeNormalRect, "일반 모드", "1층부터 로그라이크 런 시작", PixelPalette.Accent, modeNormalRect.Contains(lastMousePosition));

            if (CanStartEndlessRun)
            {
                DrawMenuActionButton(r, modeEndlessRect, "무한 모드", "667층부터 시작하는 해금 콘텐츠", PixelPalette.Info, modeEndlessRect.Contains(lastMousePosition));
            }
            else
            {
                PixelUi.Button(r, modeEndlessRect, u, "무한 모드", "666층 클리어 시 해금", PixelButtonState.Disabled);
            }

            if (CanContinueSavedRun)
            {
                DrawMenuActionButton(r, continueButtonRect, "이어하기", "저장된 런 이어서 진행", PixelPalette.Good, continueButtonRect.Contains(lastMousePosition));
            }

            DrawMenuButton(r, modeBackRect, "뒤로");
        }

        /// <summary>
        /// 게임 화면 위에 어두운 막을 씌운 일시정지 오버레이를 그린다.
        /// "게임 계속", "설정", "메인메뉴" 버튼이 포함된다.
        /// </summary>
        /// <param name="r">드로우 명령을 받을 렌더러 인스턴스.</param>
        /// <param name="width">클라이언트 영역 너비.</param>
        /// <param name="height">클라이언트 영역 높이.</param>
        private void DrawPauseOverlay(Renderer r, int width, int height)
        {
            float s = GetUiScale(width, height);
            float u = PixelUi.UnitFor(s);
            PixelUi.Dim(r, width, height, u, 150);

            // 패널 크기는 UpdatePauseLayout과 같은 상수를 사용해 버튼과 정렬을 맞춘다.
            int panelW = (int)(PausePanelWidth * s);
            int panelH = (int)(PausePanelHeight * s);
            int panelX = (width - panelW) / 2;
            int panelY = (height - panelH) / 2;

            PixelUi.Panel(r, panelX, panelY, panelW, panelH, u, "일시정지");
            DrawMenuButton(r, pauseResumeRect, "게임 계속");
            DrawMenuButton(r, pauseSettingsRect, "설정");
            DrawMenuButton(r, pauseMenuRect, "메인메뉴");
        }

        /// <summary>
        /// 사망 후 게임 월드 위에 런 요약과 다음 행동 버튼을 그린다.
        /// 런 요약은 GameLogic의 종료 시점 고정 스냅샷을 사용해 화면에 머문 시간이 플레이 시간에 섞이지 않게 한다.
        /// </summary>
        /// <param name="r">드로우 명령을 받을 렌더러 인스턴스.</param>
        /// <param name="width">클라이언트 영역 너비.</param>
        /// <param name="height">클라이언트 영역 높이.</param>
        private void DrawDeathOverlay(Renderer r, int width, int height)
        {
            float s = GetUiScale(width, height);
            float u = PixelUi.UnitFor(s);
            PixelUi.Dim(r, width, height, u, 175);

            int panelW = (int)(540 * s);
            int panelH = (int)(DeathPanelHeight * s);
            int panelX = (width - panelW) / 2;
            int panelY = (height - panelH) / 2;
            float cx = width * 0.5f;

            float contentY = PixelUi.Panel(r, panelX, panelY, panelW, panelH, u, "사망", PixelPalette.Accent);
            PixelUi.TextCentered(r, "런 요약", cx, contentY + 6f * u, PixelPalette.TextDim, PixelUi.FontBase * u * 0.5f, u);

            WorldRunSummarySnapshot summary = CaptureWorldRunSummarySnapshot();
            int rowX = panelX + (int)(58 * s);
            int valueX = panelX + panelW - (int)(58 * s);
            int rowY = panelY + (int)(116 * s);
            int rowGap = (int)(34 * s);
            DrawDeathSummaryRow(r, "도달 층", $"{summary.FloorReached}층", rowX, valueX, rowY, s);
            DrawDeathSummaryRow(r, "처치한 적", $"{summary.EnemiesKilled}명", rowX, valueX, rowY + rowGap, s);
            DrawDeathSummaryRow(r, "처치한 보스", $"{summary.BossesKilled}명", rowX, valueX, rowY + rowGap * 2, s);
            DrawDeathSummaryRow(r, "플레이 시간", FormatDuration(summary.DurationSeconds), rowX, valueX, rowY + rowGap * 3, s);

            DrawDeathCardSummary(r, summary.AcquiredCards, panelX, panelW, rowY + rowGap * 4 + (int)(6 * s), s);

            PixelUi.Button(r, deathRestartRect, u, "재시작", null,
                deathRestartRect.Contains(lastMousePosition) ? PixelButtonState.Hover : PixelButtonState.Normal, PixelPalette.Accent);
            PixelUi.Button(r, deathMenuRect, u, "메인메뉴", null,
                deathMenuRect.Contains(lastMousePosition) ? PixelButtonState.Hover : PixelButtonState.Normal, PixelPalette.Info);
        }

        /// <summary>
        /// 사망 화면의 요약 라벨/값 한 줄을 같은 열 위치에 맞춰 그린다. 값은 오른쪽 정렬이다.
        /// </summary>
        private void DrawDeathSummaryRow(Renderer r, string label, string value, int labelX, int valueX, int y, float s)
        {
            float u = PixelUi.UnitFor(s);
            float size = PixelUi.FontBase * u;
            PixelUi.Text(r, label, labelX, y, PixelPalette.TextDim, size, u);
            PixelUi.TextRight(r, value, valueX, y, PixelPalette.Text, size, u, bold: true);
            PixelUi.Divider(r, labelX, y + size + 4f * u, valueX - labelX, u, PixelPalette.WithAlpha(PixelPalette.EdgeDim, 255));
        }

        /// <summary>
        /// 사망 화면 하단에 이번 런에서 획득한 스탯 카드 목록을 2열로 그린다.
        /// 표시 한도를 넘으면 남은 종류 수를 요약 표시한다.
        /// </summary>
        private void DrawDeathCardSummary(Renderer r, string[] cards, int panelX, int panelW, int startY, float s)
        {
            float u = PixelUi.UnitFor(s);
            float cx = panelX + panelW * 0.5f;
            float small = PixelUi.FontBase * u * 0.5f;
            PixelUi.TextCentered(r, "획득한 카드", cx, startY + 8 * s, PixelPalette.Brass, small, u, bold: true);

            if (cards == null || cards.Length == 0)
            {
                PixelUi.TextCentered(r, "획득한 카드 없음", cx, startY + 36 * s, PixelPalette.TextMuted, small, u);
                return;
            }

            const int maxRows = 4;
            int maxShown = maxRows * 2;
            int shown = Math.Min(cards.Length, maxShown);
            float colLeftX = panelX + 44 * s;
            float colRightX = panelX + panelW / 2 + 10 * s;
            float listY = startY + 26 * s;
            float lineH = 18 * s;

            for (int i = 0; i < shown; i++)
            {
                float x = (i % 2 == 0) ? colLeftX : colRightX;
                float y = listY + (i / 2) * lineH;
                r.DrawRectangle(x, y + small * 0.5f - u, u * 2f, u * 2f, PixelPalette.Accent);
                PixelUi.Text(r, cards[i], x + u * 5f, y, PixelPalette.Text, small, u);
            }

            if (cards.Length > shown)
            {
                PixelUi.TextCentered(r, $"+{cards.Length - shown}종 더", cx, listY + maxRows * lineH + small * 0.5f,
                    PixelPalette.TextDim, small, u);
            }
        }

        /// <summary>
        /// 설정 화면을 그린다. 시야각·감도·BGM·효과음 슬라이더와 창 크기 선택, 뒤로 버튼.
        /// 일시정지 중 열린 설정이면 게임 화면 위에 어두운 막을 씌워 표시된다.
        /// </summary>
        /// <param name="r">드로우 명령을 받을 렌더러 인스턴스.</param>
        /// <param name="width">클라이언트 영역 너비.</param>
        /// <param name="height">클라이언트 영역 높이.</param>
        /// <param name="overlay">true이면 게임 위 오버레이, false이면 메뉴 배경 위에 그린다.</param>
        private void DrawSettings(Renderer r, int width, int height, bool overlay)
        {
            WorldSettingsSnapshot settings = GetDisplayedSettings();
            float s = GetUiScale(width, height);
            float u = PixelUi.UnitFor(s);

            if (overlay)
            {
                PixelUi.Dim(r, width, height, u, 150);
            }
            else
            {
                DrawSharedMenuBackground(r, width, height);
            }

            int panelW = (int)(620 * s);
            // 패널 높이는 UpdateSettingsLayout과 동일하게 590 기준이어야 모든 슬라이더/버튼을 감싼다.
            int panelH = (int)(590 * s);
            int panelX = (width - panelW) / 2;
            int panelY = (height - panelH) / 2;
            PixelUi.Panel(r, panelX, panelY, panelW, panelH, u, "설정");

            float labelSize = PixelUi.FontBase * u * 0.5f;
            float valueSize = PixelUi.FontBase * u;
            float labelOffY = labelSize + 8f * u;

            DrawSettingSlider(r, fovSliderRect, "시야각 (60 - 90)", settings.FovDegrees.ToString("0"), GetRatio(settings.FovDegrees, FovMin, FovMax), u, labelSize, valueSize, labelOffY);
            DrawSettingSlider(r, sensSliderRect, "마우스 감도", settings.MouseSensitivity.ToString("0.000"), GetRatio(settings.MouseSensitivity, SensMin, SensMax), u, labelSize, valueSize, labelOffY);

            // 창 크기: < [값] >
            PixelUi.Text(r, "창 크기 (16:9 고정)", resolutionPrevRect.X, resolutionPrevRect.Y - labelOffY, PixelPalette.TextDim, labelSize, u);
            DrawMenuButton(r, resolutionPrevRect, "<");
            DrawMenuButton(r, resolutionNextRect, ">");
            float boxX = resolutionPrevRect.Right + 6f * u;
            float boxW = resolutionNextRect.X - resolutionPrevRect.Right - 12f * u;
            PixelUi.Frame(r, boxX, resolutionPrevRect.Y, boxW, resolutionPrevRect.Height, u, PixelPalette.PanelDeep, PixelPalette.EdgeDim, raised: false);
            PixelUi.TextCentered(r, GetCurrentWindowSizeLabel(), width * 0.5f, resolutionPrevRect.Y + resolutionPrevRect.Height * 0.5f, PixelPalette.Text, valueSize, u, bold: true);

            DrawSettingSlider(r, bgmSliderRect, "BGM 볼륨", settings.BgmVolume.ToString(), settings.BgmVolume / 100f, u, labelSize, valueSize, labelOffY);
            DrawSettingSlider(r, sfxSliderRect, "효과음 볼륨", settings.SfxVolume.ToString(), settings.SfxVolume / 100f, u, labelSize, valueSize, labelOffY);

            DrawMenuButton(r, backButtonRect, "뒤로");
        }

        /// <summary>설정 화면의 라벨 + 슬라이더 + 오른쪽 값 한 줄.</summary>
        private void DrawSettingSlider(Renderer r, Rectangle rect, string label, string value, float ratio, float u, float labelSize, float valueSize, float labelOffY)
        {
            PixelUi.Text(r, label, rect.X, rect.Y - labelOffY, PixelPalette.TextDim, labelSize, u);
            Rectangle hitArea = Rectangle.Inflate(rect, 0, (int)(6 * u));
            PixelUi.Slider(r, rect, u, Clamp01(ratio), hitArea.Contains(lastMousePosition));
            PixelUi.Text(r, value, rect.Right + 10f * u, rect.Y + rect.Height * 0.5f - valueSize * 0.5f, PixelPalette.Text, valueSize, u, bold: true);
        }

        /// <summary>
        /// 메뉴 계열 독립 화면(메인 메뉴·모드 선택·기록·설정)의 공통 배경을 그린다.
        /// 게임 위에 겹쳐 그리는 일시정지·사망 오버레이에는 사용하지 않는다.
        /// </summary>
        private void DrawSharedMenuBackground(Renderer r, int width, int height)
        {
            PixelUi.Backdrop(r, width, height, PixelUi.UnitFor(GetUiScale(width, height)), MenuTime);
        }

        /// <summary>
        /// 제목과 설명을 가진 액션 버튼을 그린다. 마우스가 올라가면 강조 상태로 그린다.
        /// </summary>
        /// <param name="r">드로우 명령을 받을 렌더러 인스턴스.</param>
        /// <param name="rect">버튼이 차지할 화면 영역.</param>
        /// <param name="title">버튼의 주 제목 텍스트 (예: "게임 시작").</param>
        /// <param name="subtitle">버튼의 설명 텍스트 (예: "모드 선택 후 전장 진입").</param>
        /// <param name="accentColor">왼쪽 표식과 강조 테두리에 사용할 색.</param>
        /// <param name="highlighted">마우스 커서가 버튼 위에 있을 때 true.</param>
        private void DrawMenuActionButton(Renderer r, Rectangle rect, string title, string subtitle, Color accentColor, bool highlighted)
        {
            float u = PixelUi.UnitFor(GetUiScale(ClientSize.Width, ClientSize.Height));
            PixelUi.Button(r, rect, u, title, subtitle, highlighted ? PixelButtonState.Hover : PixelButtonState.Normal, accentColor);
        }

        /// <summary>
        /// 글자 하나만 있는 버튼을 그린다. hover는 커서 위치로 내부 판정한다.
        /// </summary>
        /// <param name="r">드로우 명령을 받을 렌더러 인스턴스.</param>
        /// <param name="rect">버튼이 차지할 화면 영역.</param>
        /// <param name="text">버튼 중앙에 표시할 텍스트.</param>
        private void DrawMenuButton(Renderer r, Rectangle rect, string text)
        {
            float u = PixelUi.UnitFor(GetUiScale(ClientSize.Width, ClientSize.Height));
            PixelUi.Button(r, rect, u, text, null, rect.Contains(lastMousePosition) ? PixelButtonState.Hover : PixelButtonState.Normal);
        }

        /// <summary>
        /// 메인 메뉴 버튼들의 위치와 크기를 현재 창 크기에 맞게 계산한다.
        /// 버튼은 수직 방향으로 일정 간격을 두고 화면 중앙에 정렬된다.
        /// </summary>
        /// <param name="width">클라이언트 영역 너비.</param>
        /// <param name="height">클라이언트 영역 높이.</param>
        private void UpdateMenuLayout(int width, int height)
        {
            float s = GetUiScale(width, height);
            int buttonW = (int)(360 * s);
            int buttonH = (int)(68 * s);
            int centerX = width / 2;
            int spacing = (int)(18 * s);

            int startY = (int)(height * 0.50f);
            startButtonRect    = new Rectangle(centerX - buttonW / 2, startY,                          buttonW, buttonH);
            settingsButtonRect = new Rectangle(centerX - buttonW / 2, startY + (buttonH + spacing),     buttonW, buttonH);
            recordsButtonRect  = new Rectangle(centerX - buttonW / 2, startY + (buttonH + spacing) * 2, buttonW, buttonH);
            exitButtonRect     = new Rectangle(centerX - buttonW / 2, startY + (buttonH + spacing) * 3, buttonW, buttonH);
        }

        /// <summary>
        /// 모드 선택 화면 버튼들의 위치와 크기를 현재 창 크기에 맞게 계산한다.
        /// 패널 중앙을 기준으로 일반 모드/무한 모드/뒤로 버튼의 영역을 설정한다.
        /// </summary>
        /// <param name="width">클라이언트 영역 너비.</param>
        /// <param name="height">클라이언트 영역 높이.</param>
        private void UpdateModeLayout(int width, int height)
        {
            float s = GetUiScale(width, height);
            int panelH = GetModePanelHeight(s);
            int panelY = (height - panelH) / 2;

            int buttonW = (int)(340 * s);
            int buttonH = (int)(60 * s);
            int spacing = (int)(14 * s);
            int centerX = width / 2;
            int startY = panelY + (int)(126 * s);

            modeNormalRect  = new Rectangle(centerX - buttonW / 2, startY,                          buttonW, buttonH);
            modeEndlessRect = new Rectangle(centerX - buttonW / 2, startY + (buttonH + spacing),     buttonW, buttonH);
            continueButtonRect = new Rectangle(centerX - buttonW / 2, startY + (buttonH + spacing) * 2, buttonW, buttonH);
            modeBackRect    = new Rectangle(centerX - (int)(96 * s), panelY + panelH - (int)(62 * s), (int)(192 * s), (int)(46 * s));
        }

        /// <summary>
        /// 이어하기 버튼 표시 여부에 따라 모드 선택 패널 높이를 조정한다.
        /// </summary>
        private int GetModePanelHeight(float s)
        {
            return (int)((CanContinueSavedRun ? 490 : 420) * s);
        }

        /// <summary>
        /// 일시정지 화면 버튼들의 위치와 크기를 현재 창 크기에 맞게 계산한다.
        /// 게임 계속/설정/메인메뉴 버튼이 화면 중앙에 수직으로 배치된다.
        /// </summary>
        /// <param name="width">클라이언트 영역 너비.</param>
        /// <param name="height">클라이언트 영역 높이.</param>
        /// <summary>일시정지 패널의 기준 너비/높이(스케일 적용 전). DrawPauseOverlay와 공유한다.</summary>
        private const float PausePanelWidth = 380f;
        private const float PausePanelHeight = 340f;

        private void UpdatePauseLayout(int width, int height)
        {
            float s = GetUiScale(width, height);
            float panelH = PausePanelHeight * s;
            float panelY = (height - panelH) * 0.5f;

            int buttonW = (int)(250 * s);
            int buttonH = (int)(52 * s);
            int centerX = width / 2;
            int startY = (int)(panelY + 108 * s);
            int spacing = (int)(16 * s);

            pauseResumeRect = new Rectangle(centerX - buttonW / 2, startY, buttonW, buttonH);
            pauseSettingsRect = new Rectangle(centerX - buttonW / 2, startY + buttonH + spacing, buttonW, buttonH);
            pauseMenuRect = new Rectangle(centerX - buttonW / 2, startY + (buttonH + spacing) * 2, buttonW, buttonH);
        }

        /// <summary>
        /// 사망 오버레이의 버튼 영역을 현재 창 크기에 맞춰 계산한다.
        /// 버튼 위치는 DrawDeathOverlay의 패널 계산과 같은 식을 사용해야 클릭 영역과 렌더 위치가 어긋나지 않는다.
        /// </summary>
        /// <param name="width">클라이언트 영역 너비.</param>
        /// <param name="height">클라이언트 영역 높이.</param>
        /// <summary>사망 오버레이 패널 높이. UpdateDeathLayout과 DrawDeathOverlay가 공유한다.</summary>
        private const int DeathPanelHeight = 460;

        private void UpdateDeathLayout(int width, int height)
        {
            float s = GetUiScale(width, height);
            int panelW = (int)(540 * s);
            int panelH = (int)(DeathPanelHeight * s);
            int panelX = (width - panelW) / 2;
            int panelY = (height - panelH) / 2;
            int buttonH = (int)(50 * s);
            int buttonW = (int)(210 * s);
            int buttonY = panelY + panelH - (int)(76 * s);

            deathRestartRect = new Rectangle(panelX + (int)(28 * s), buttonY, buttonW, buttonH);
            deathMenuRect = new Rectangle(panelX + panelW - (int)(28 * s) - buttonW, buttonY, buttonW, buttonH);
        }

        /// <summary>
        /// 설정 화면 컨트롤들의 위치와 크기를 현재 창 크기에 맞게 계산한다.
        /// FOV 슬라이더, 감도 슬라이더, 창 크기 이전/다음 버튼, 뒤로 버튼의 영역을 설정한다.
        /// </summary>
        /// <param name="width">클라이언트 영역 너비.</param>
        /// <param name="height">클라이언트 영역 높이.</param>
        private void UpdateSettingsLayout(int width, int height)
        {
            float s = GetUiScale(width, height);
            int panelW = (int)(620 * s);
            int panelH = (int)(590 * s);
            int panelX = (width - panelW) / 2;
            int panelY = (height - panelH) / 2;

            int sliderW = panelW - (int)(180 * s);
            int sliderH = Math.Max(8, (int)(12 * s));
            int sliderX = panelX + (int)(36 * s);
            int firstY = panelY + (int)(112 * s);
            int spacing = (int)(84 * s);

            fovSliderRect = new Rectangle(sliderX, firstY, sliderW, sliderH);
            sensSliderRect = new Rectangle(sliderX, firstY + spacing, sliderW, sliderH);

            int resolutionY = sensSliderRect.Y + spacing;
            int resolutionButtonW = (int)(54 * s);
            int resolutionButtonH = (int)(42 * s);
            resolutionPrevRect = new Rectangle(panelX + (int)(36 * s), resolutionY, resolutionButtonW, resolutionButtonH);
            resolutionNextRect = new Rectangle(panelX + panelW - (int)(36 * s) - resolutionButtonW, resolutionY, resolutionButtonW, resolutionButtonH);

            int bgmY = resolutionY + resolutionButtonH + (int)(42 * s);
            bgmSliderRect = new Rectangle(sliderX, bgmY, sliderW, sliderH);
            sfxSliderRect = new Rectangle(sliderX, bgmY + spacing, sliderW, sliderH);

            backButtonRect = new Rectangle(panelX + panelW / 2 - (int)(96 * s), panelY + panelH - (int)(72 * s), (int)(192 * s), (int)(46 * s));
        }

        /// <summary>
        /// 메인 메뉴 화면에서의 클릭을 처리한다.
        /// 클릭 위치에 따라 모드 선택 화면 열기, 설정 화면 열기, 또는 게임 종료를 수행한다.
        /// </summary>
        /// <param name="location">클릭한 클라이언트 좌표.</param>
        private void HandleMenuClick(Point location)
        {
            EnsureUiLayout(ClientSize.Width, ClientSize.Height);

            if (startButtonRect.Contains(location))
            {
                OpenModeSelect();
                return;
            }

            if (settingsButtonRect.Contains(location))
            {
                OpenSettingsFromMenu();
                return;
            }

            if (recordsButtonRect.Contains(location))
            {
                EnterMenuState(GameState.Records);
                return;
            }

            if (exitButtonRect.Contains(location))
            {
                Close();
            }
        }

        /// <summary>
        /// 모드 선택 화면에서의 클릭을 처리한다.
        /// 일반 모드 또는 무한 모드를 시작하거나, 뒤로 버튼을 클릭하면 메인 메뉴로 돌아간다.
        /// </summary>
        /// <param name="location">클릭한 클라이언트 좌표.</param>
        private void HandleModeClick(Point location)
        {
            UpdateModeLayout(ClientSize.Width, ClientSize.Height);

            if (modeNormalRect.Contains(location))
            {
                StartNormalGame();
                return;
            }

            if (CanStartEndlessRun && modeEndlessRect.Contains(location))
            {
                StartEndlessGame();
                return;
            }

            if (CanContinueSavedRun && continueButtonRect.Contains(location))
            {
                ContinueGame();
                return;
            }

            if (modeBackRect.Contains(location))
            {
                EnterMenuState(GameState.Menu);
            }
        }

        /// <summary>
        /// 일시정지 화면에서의 클릭을 처리한다.
        /// 게임 계속, 설정 열기, 메인 메뉴로 이동 중 하나를 수행한다.
        /// </summary>
        /// <param name="location">클릭한 클라이언트 좌표.</param>
        private void HandlePauseClick(Point location)
        {
            EnsureUiLayout(ClientSize.Width, ClientSize.Height);

            if (pauseResumeRect.Contains(location))
            {
                ResumeGame();
                return;
            }

            if (pauseSettingsRect.Contains(location))
            {
                OpenSettingsFromPause();
                return;
            }

            if (pauseMenuRect.Contains(location))
            {
                GoToMenu();
            }
        }

        /// <summary>
        /// 사망 오버레이 클릭을 처리한다.
        /// 버튼 밖 클릭도 소비해 사망 상태에서 월드 발사 입력으로 떨어지지 않게 한다.
        /// </summary>
        /// <param name="location">클릭한 클라이언트 좌표.</param>
        /// <returns>사망 오버레이가 클릭을 소비했으면 true.</returns>
        private bool HandleDeathClick(Point location)
        {
            EnsureUiLayout(ClientSize.Width, ClientSize.Height);

            if (deathRestartRect.Contains(location))
            {
                RestartDeadRun();
                return true;
            }

            if (deathMenuRect.Contains(location))
            {
                GoToMenu();
                return true;
            }

            return true;
        }

        /// <summary>
        /// 설정 화면에서의 클릭을 처리한다.
        /// 뒤로 버튼, FOV 슬라이더, 감도 슬라이더, 창 크기 이전/다음 버튼 클릭을 구분하여 처리한다.
        /// 슬라이더 클릭 시 드래그 상태를 시작하고 즉시 값을 적용한다.
        /// </summary>
        /// <param name="location">클릭한 클라이언트 좌표.</param>
        private void HandleSettingsClick(Point location)
        {
            EnsureUiLayout(ClientSize.Width, ClientSize.Height);

            if (backButtonRect.Contains(location))
            {
                ExitSettings();
                return;
            }

            if (fovSliderRect.Contains(location))
            {
                draggingFov = true;
                SetFovFromMouse(location.X);
            }
            else if (sensSliderRect.Contains(location))
            {
                draggingSensitivity = true;
                SetSensitivityFromMouse(location.X);
            }
            else if (bgmSliderRect.Contains(location))
            {
                draggingBgm = true;
                SetBgmVolumeFromMouse(location.X);
            }
            else if (sfxSliderRect.Contains(location))
            {
                draggingSfx = true;
                SetSfxVolumeFromMouse(location.X);
            }
            else if (resolutionPrevRect.Contains(location))
            {
                ChangeWindowSizePreset(-1);
            }
            else if (resolutionNextRect.Contains(location))
            {
                ChangeWindowSizePreset(1);
            }
        }

        /// <summary>
        /// 마우스 X 좌표를 FOV 슬라이더 범위에 매핑하여 시야각을 설정한다.
        /// 슬라이더 영역 밖의 좌표는 자동으로 클램핑된다.
        /// </summary>
        /// <param name="mouseX">마우스의 클라이언트 X 좌표.</param>
        private void SetFovFromMouse(int mouseX)
        {
            float t = (mouseX - fovSliderRect.X) / (float)fovSliderRect.Width;
            t = Clamp01(t);
            float value = FovMin + (t * (FovMax - FovMin));
            pendingSettings = pendingSettings with { FovDegrees = value };
            MarkSettingsDirty();
        }

        /// <summary>
        /// 마우스 X 좌표를 감도 슬라이더 범위에 매핑하여 마우스 감도를 설정한다.
        /// 슬라이더 영역 밖의 좌표는 자동으로 클램핑된다.
        /// </summary>
        /// <param name="mouseX">마우스의 클라이언트 X 좌표.</param>
        private void SetSensitivityFromMouse(int mouseX)
        {
            float t = (mouseX - sensSliderRect.X) / (float)sensSliderRect.Width;
            t = Clamp01(t);
            float value = SensMin + (t * (SensMax - SensMin));
            pendingSettings = pendingSettings with { MouseSensitivity = value };
            MarkSettingsDirty();
        }

        /// <summary>
        /// 마우스 X 좌표를 BGM 볼륨 슬라이더 범위에 매핑하여 BGM 볼륨을 설정한다.
        /// 슬라이더 영역 밖의 좌표는 자동으로 클램핑된다.
        /// </summary>
        /// <param name="mouseX">마우스의 클라이언트 X 좌표.</param>
        private void SetBgmVolumeFromMouse(int mouseX)
        {
            float t = (mouseX - bgmSliderRect.X) / (float)bgmSliderRect.Width;
            t = Clamp01(t);
            int volume = (int)Math.Round(t * 100f);
            pendingSettings = pendingSettings with { BgmVolume = volume };
            MarkSettingsDirty();
        }

        /// <summary>
        /// 마우스 X 좌표를 효과음 볼륨 슬라이더 범위에 매핑하여 효과음 볼륨을 설정한다.
        /// 슬라이더 영역 밖의 좌표는 자동으로 클램핑된다.
        /// </summary>
        /// <param name="mouseX">마우스의 클라이언트 X 좌표.</param>
        private void SetSfxVolumeFromMouse(int mouseX)
        {
            float t = (mouseX - sfxSliderRect.X) / (float)sfxSliderRect.Width;
            t = Clamp01(t);
            int volume = (int)Math.Round(t * 100f);
            pendingSettings = pendingSettings with { SfxVolume = volume };
            MarkSettingsDirty();
        }

        /// <summary>
        /// value를 [min, max] 범위 내에서 0 ~ 1 비율로 정규화하여 반환한다.
        /// 슬라이더의 현재 값을 드로우 비율로 변환할 때 사용된다.
        /// </summary>
        /// <param name="value">정규화할 현재 값.</param>
        /// <param name="min">범위 최솟값.</param>
        /// <param name="max">범위 최댓값.</param>
        /// <returns>0.0 ~ 1.0 범위의 정규화된 비율. max &lt;= min이면 0을 반환한다.</returns>
        private float GetRatio(float value, float min, float max)
        {
            if (max <= min)
            {
                return 0f;
            }

            return (value - min) / (max - min);
        }

        /// <summary>
        /// value를 0.0 ~ 1.0 범위로 클램핑하여 반환한다.
        /// 슬라이더 비율이 범위를 벗어나지 않도록 보정할 때 사용된다.
        /// </summary>
        /// <param name="value">클램핑할 값.</param>
        /// <returns>0.0 미만이면 0.0, 1.0 초과이면 1.0, 그 외에는 원래 값.</returns>
        private float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        private void DrawRecords(Renderer r, int width, int height)
        {
            DrawSharedMenuBackground(r, width, height);

            float s = GetUiScale(width, height);
            float u = PixelUi.UnitFor(s);
            float cx = width * 0.5f;
            float panelW = 740f * s;
            float panelH = 520f * s;
            float panelX = cx - panelW * 0.5f;
            float panelY = (height - panelH) * 0.5f;
            float small = PixelUi.FontBase * u * 0.5f;

            float contentY = PixelUi.Panel(r, panelX, panelY, panelW, panelH, u, "기록", PixelPalette.Info);

            WorldRunRecordSnapshot[] records = LoadWorldRunRecords(15);

            // 정렬 적용: 최신순(0)은 DB 기본 순서를 유지하고, 그 외는 기준값 내림차순으로 정렬한다.
            if (recordsSortMode != 0 && records.Length > 1)
            {
                Array.Sort(records, (a, b) =>
                {
                    switch (recordsSortMode)
                    {
                        case 1: return b.FloorReached.CompareTo(a.FloorReached);
                        case 2: return b.EnemiesKilled.CompareTo(a.EnemiesKilled);
                        case 3: return b.DurationSeconds.CompareTo(a.DurationSeconds);
                        default: return 0;
                    }
                });
            }

            if (records.Length == 0)
            {
                recordsSortRect = Rectangle.Empty;
                PixelUi.TextCentered(r, "아직 도전 기록이 없습니다.", cx, panelY + panelH * 0.5f, PixelPalette.TextDim, PixelUi.FontBase * u, u);
            }
            else
            {
                int sortBtnW = (int)(150 * s), sortBtnH = (int)(30 * s);
                recordsSortRect = new Rectangle((int)(panelX + panelW - sortBtnW - 16f * u), (int)contentY, sortBtnW, sortBtnH);
                PixelUi.Button(r, recordsSortRect, u, "정렬: " + GetRecordsSortLabel(), null,
                    recordsSortRect.Contains(lastMousePosition) ? PixelButtonState.Hover : PixelButtonState.Normal, PixelPalette.Info);

                int bestFloor = 0, bestKills = 0, victoryCount = 0;
                foreach (var rec in records)
                {
                    if (rec.FloorReached > bestFloor) bestFloor = rec.FloorReached;
                    if (rec.EnemiesKilled > bestKills) bestKills = rec.EnemiesKilled;
                    if (rec.IsVictory) victoryCount++;
                }

                string summaryText = $"총 {records.Length}회 도전 · 최고 {bestFloor}층 · 최고 처치 {bestKills}명";
                if (victoryCount > 0)
                    summaryText += $" · 클리어 {victoryCount}회";
                PixelUi.Text(r, summaryText, panelX + 16f * u, contentY + (sortBtnH - small) * 0.5f,
                    victoryCount > 0 ? PixelPalette.Good : PixelPalette.TextDim, small, u);

                float headerY = contentY + sortBtnH + 10f * u;
                float colFlag   = panelX + panelW * 0.04f;
                float colFloor  = panelX + panelW * 0.10f;
                float colEnemy  = panelX + panelW * 0.28f;
                float colBoss   = panelX + panelW * 0.45f;
                float colTime   = panelX + panelW * 0.62f;
                float colDate   = panelX + panelW * 0.80f;

                r.DrawRectangle(panelX + 8f * u, headerY - 3f * u, panelW - 16f * u, small + 6f * u, PixelPalette.Darken(PixelPalette.Info, 0.75f));
                PixelUi.Text(r, "도달 층", colFloor, headerY, PixelPalette.Brass, small, u, bold: true);
                PixelUi.Text(r, "적 처치", colEnemy, headerY, PixelPalette.Brass, small, u, bold: true);
                PixelUi.Text(r, "보스 처치", colBoss, headerY, PixelPalette.Brass, small, u, bold: true);
                PixelUi.Text(r, "플레이 시간", colTime, headerY, PixelPalette.Brass, small, u, bold: true);
                PixelUi.Text(r, "날짜", colDate, headerY, PixelPalette.Brass, small, u, bold: true);

                float rowH = small + 10f * u;
                float rowStart = headerY + small + 10f * u;
                for (int i = 0; i < records.Length; i++)
                {
                    var rec = records[i];
                    float rowY = rowStart + i * rowH;
                    if (rowY + rowH > panelY + panelH - 64f * s) break;

                    if (i % 2 == 0)
                        r.DrawRectangle(panelX + 8f * u, rowY - 4f * u, panelW - 16f * u, rowH, Color.FromArgb(60, 0, 0, 0));

                    Color rowCol = i == 0 ? PixelPalette.Brass : PixelPalette.Text;
                    if (rec.IsVictory)
                        PixelUi.Text(r, "★", colFlag, rowY, PixelPalette.Good, small, u);
                    PixelUi.Text(r, $"{rec.FloorReached}층", colFloor, rowY, rowCol, small, u);
                    PixelUi.Text(r, $"{rec.EnemiesKilled}명", colEnemy, rowY, rowCol, small, u);
                    PixelUi.Text(r, $"{rec.BossesKilled}명", colBoss, rowY, rowCol, small, u);
                    PixelUi.Text(r, FormatDuration(rec.DurationSeconds), colTime, rowY, rowCol, small, u);
                    PixelUi.Text(r, rec.EndedAt, colDate, rowY, PixelPalette.TextDim, small, u);
                }
            }

            int backW = (int)(180 * s), backH = (int)(44 * s);
            recordsBackRect = new Rectangle((int)(cx - backW * 0.5f), (int)(panelY + panelH - 58f * s), backW, backH);
            DrawMenuButton(r, recordsBackRect, "뒤로");
        }

        private void HandleRecordsClick(Point location)
        {
            if (recordsSortRect != Rectangle.Empty && recordsSortRect.Contains(location))
            {
                // 최신 → 도달 층 → 적 처치 → 플레이 시간 순으로 순환한다.
                recordsSortMode = (recordsSortMode + 1) % 4;
                return;
            }

            if (recordsBackRect.Contains(location))
                EnterMenuState(GameState.Menu);
        }

        /// <summary>현재 기록 정렬 기준의 표시용 라벨을 반환한다.</summary>
        private string GetRecordsSortLabel()
        {
            switch (recordsSortMode)
            {
                case 1: return "도달 층";
                case 2: return "적 처치";
                case 3: return "플레이 시간";
                default: return "최신";
            }
        }

        private static string FormatDuration(int seconds)
        {
            int m = seconds / 60;
            int s = seconds % 60;
            return m > 0 ? $"{m}분 {s:00}초" : $"{s}초";
        }
    }
}
