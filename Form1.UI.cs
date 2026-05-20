using System;
using System.Drawing;
using My2DEngine.Engine.Rendering;
namespace My2DEngine
{
    /// <summary>
    /// Form1의 UI 렌더링 partial 클래스.
    /// 메인 메뉴, 모드 선택, 설정, 일시정지 오버레이의 레이아웃 계산과 드로우를 담당한다.
    /// </summary>
    public partial class Form1
    {
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

        /// <summary>
        /// 메인 메뉴 화면을 그린다. 배경, 로고(또는 텍스트 타이틀), 부제목,
        /// 게임 시작/설정/종료 버튼을 순서대로 렌더링한다.
        /// 마우스 커서 위치를 기반으로 버튼 hover 효과를 적용한다.
        /// </summary>
        /// <param name="r">드로우 명령을 받을 렌더러 인스턴스.</param>
        /// <param name="width">클라이언트 영역 너비 (레이아웃 기준).</param>
        /// <param name="height">클라이언트 영역 높이 (레이아웃 기준).</param>
        private void DrawMenu(Renderer r, int width, int height)
        {
            DrawMenuBackdrop(r, width, height);

            if (menuLogo != null)
            {
                float maxLogoW = Math.Min(width * 0.86f, 760f);
                float maxLogoH = Math.Min(height * 0.34f, 300f);
                float scale = Math.Min(maxLogoW / menuLogo.Width, maxLogoH / menuLogo.Height);
                float logoW = menuLogo.Width * scale;
                float logoH = menuLogo.Height * scale;
                float logoX = (width - logoW) * 0.5f;
                float logoY = height * 0.11f;
                r.DrawImage(menuLogo, logoX, logoY, logoW, logoH);
            }
            else
            {
                float titleY = height * 0.28f;
                r.DrawTextCenteredShadow("KILLING FIELD", width * 0.5f, titleY, Color.FromArgb(230, 180, 42, 42), 44f);
            }

            r.DrawTextCenteredShadow("ABANDONED SECTOR SURVIVAL", width * 0.5f, height * 0.42f, Color.FromArgb(210, 210, 210, 210), 14f);

            DrawMenuActionButton(r, startButtonRect, "게임 시작", "모드 선택 후 전장 진입", Color.FromArgb(210, 130, 36, 36), startButtonRect.Contains(lastMousePosition));
            DrawMenuActionButton(r, settingsButtonRect, "설정", "시야각과 마우스 감도 조정", Color.FromArgb(210, 92, 96, 104), settingsButtonRect.Contains(lastMousePosition));
            DrawMenuActionButton(r, recordsButtonRect, "기록", "역대 도전 기록 보기", Color.FromArgb(210, 60, 90, 130), recordsButtonRect.Contains(lastMousePosition));
            DrawMenuActionButton(r, exitButtonRect, "게임 종료", "세션 종료", Color.FromArgb(210, 88, 52, 52), exitButtonRect.Contains(lastMousePosition));

            r.DrawText("[I]  영구 스탯 보기", 24f, height * 0.93f, Color.FromArgb(180, 190, 190, 190), 11f);
        }

        /// <summary>
        /// 모드 선택 화면을 그린다. 배경, 패널, 제목, 설명 텍스트,
        /// 일반 모드/무한 모드/뒤로 버튼을 렌더링한다.
        /// </summary>
        /// <param name="r">드로우 명령을 받을 렌더러 인스턴스.</param>
        /// <param name="width">클라이언트 영역 너비.</param>
        /// <param name="height">클라이언트 영역 높이.</param>
        private void DrawModeSelect(Renderer r, int width, int height)
        {
            DrawMenuBackdrop(r, width, height);

            int panelW = Math.Min(600, Math.Max(400, width - 120));
            int panelH = GetModePanelHeight();
            int panelX = (width - panelW) / 2;
            int panelY = (height - panelH) / 2;

            r.DrawRectangle(panelX, panelY, panelW, panelH, Color.FromArgb(220, 24, 24, 24));
            r.DrawRectangle(panelX + 10, panelY + 10, panelW - 20, 6, Color.FromArgb(220, 140, 34, 34));
            r.DrawTextCenteredShadow("진행 모드 선택", width * 0.5f, panelY + 48, Color.White, 26f);
            r.DrawTextCenteredShadow("전투 밸런스는 기본값으로 고정됩니다", width * 0.5f, panelY + 86, Color.Gainsboro, 14f);

            DrawMenuActionButton(r, modeNormalRect, "일반 모드", "1층부터 로그라이크 런 시작", Color.FromArgb(210, 150, 118, 48), modeNormalRect.Contains(lastMousePosition));

            if (CanStartEndlessRun)
            {
                DrawMenuActionButton(r, modeEndlessRect, "무한 모드", "667층부터 시작하는 해금 콘텐츠", Color.FromArgb(210, 70, 118, 162), modeEndlessRect.Contains(lastMousePosition));
            }
            else
            {
                r.DrawRectangle(modeEndlessRect.X, modeEndlessRect.Y, modeEndlessRect.Width, modeEndlessRect.Height, Color.FromArgb(200, 34, 34, 34));
                r.DrawRectangle(modeEndlessRect.X + 8, modeEndlessRect.Y + 8, 8, modeEndlessRect.Height - 16, Color.FromArgb(140, 90, 90, 90));
                r.DrawText("무한 모드", modeEndlessRect.X + 36, modeEndlessRect.Y + 12, Color.FromArgb(170, 205, 205, 205), 17f);
                r.DrawText("666층 클리어 시 해금", modeEndlessRect.X + 36, modeEndlessRect.Y + 38, Color.FromArgb(170, 165, 165, 165), 10.5f);
            }

            if (CanContinueSavedRun)
            {
                DrawMenuActionButton(r, continueButtonRect, "이어하기", "저장된 런 이어서 진행", Color.FromArgb(210, 46, 110, 62), continueButtonRect.Contains(lastMousePosition));
            }

            DrawMenuButton(r, modeBackRect, "뒤로", modeBackRect.Contains(lastMousePosition) ? Color.FromArgb(225, 74, 74, 74) : Color.FromArgb(210, 42, 42, 42));
        }

        /// <summary>
        /// 게임 화면 위에 반투명 배경을 씌운 일시정지 오버레이를 그린다.
        /// "게임 계속", "설정", "메인메뉴" 버튼이 포함된다.
        /// </summary>
        /// <param name="r">드로우 명령을 받을 렌더러 인스턴스.</param>
        /// <param name="width">클라이언트 영역 너비.</param>
        /// <param name="height">클라이언트 영역 높이.</param>
        private void DrawPauseOverlay(Renderer r, int width, int height)
        {
            r.DrawRectangle(0, 0, width, height, Color.FromArgb(160, 0, 0, 0));

            int panelW = 440;
            int panelH = 310;
            int panelX = (width - panelW) / 2;
            int panelY = (height - panelH) / 2;
            r.DrawRectangle(panelX, panelY, panelW, panelH, Color.FromArgb(220, 30, 30, 30));

            r.DrawTextCenteredShadow("일시정지", width * 0.5f, panelY + 42, Color.White, 24f);
            DrawMenuButton(r, pauseResumeRect, "게임 계속");
            DrawMenuButton(r, pauseSettingsRect, "설정");
            DrawMenuButton(r, pauseMenuRect, "메인메뉴");
        }

        private void DrawDeathOverlay(Renderer r, int width, int height)
        {
            r.DrawRectangle(0, 0, width, height, Color.FromArgb(185, 0, 0, 0));

            int panelW = Math.Min(540, Math.Max(380, width - 120));
            int panelH = 340;
            int panelX = (width - panelW) / 2;
            int panelY = (height - panelH) / 2;
            float cx = width * 0.5f;

            r.DrawRectangle(panelX, panelY, panelW, panelH, Color.FromArgb(236, 24, 20, 20));
            r.DrawRectangle(panelX, panelY, panelW, 4, Color.FromArgb(230, 160, 38, 38));
            r.DrawRectangle(panelX + 24, panelY + 86, panelW - 48, 1, Color.FromArgb(100, 255, 255, 255));

            r.DrawTextCenteredShadow("사망", cx, panelY + 34, Color.FromArgb(255, 255, 222, 222), 26f);
            r.DrawTextCenteredShadow("런 요약", cx, panelY + 72, Color.FromArgb(230, 230, 230, 230), 12f);

            WorldRunSummarySnapshot summary = CaptureWorldRunSummarySnapshot();
            int rowX = panelX + 58;
            int valueX = panelX + panelW - 58;
            int rowY = panelY + 116;
            int rowGap = 34;
            DrawDeathSummaryRow(r, "도달 층", $"{summary.FloorReached}층", rowX, valueX, rowY);
            DrawDeathSummaryRow(r, "처치한 적", $"{summary.EnemiesKilled}명", rowX, valueX, rowY + rowGap);
            DrawDeathSummaryRow(r, "처치한 보스", $"{summary.BossesKilled}명", rowX, valueX, rowY + rowGap * 2);
            DrawDeathSummaryRow(r, "플레이 시간", FormatDuration(summary.DurationSeconds), rowX, valueX, rowY + rowGap * 3);

            DrawMenuButton(
                r,
                deathRestartRect,
                "재시작",
                deathRestartRect.Contains(lastMousePosition)
                    ? Color.FromArgb(230, 130, 48, 42)
                    : Color.FromArgb(215, 92, 36, 34));
            DrawMenuButton(
                r,
                deathMenuRect,
                "메인메뉴",
                deathMenuRect.Contains(lastMousePosition)
                    ? Color.FromArgb(230, 70, 84, 108)
                    : Color.FromArgb(215, 44, 54, 74));
        }

        private void DrawDeathSummaryRow(Renderer r, string label, string value, int labelX, int valueX, int y)
        {
            r.DrawText(label, labelX, y, Color.FromArgb(205, 188, 188, 188), 12f);
            r.DrawText(value, valueX - 120, y, Color.FromArgb(245, 245, 245, 245), 12f);
        }

        /// <summary>
        /// 설정 화면을 그린다. 시야각 슬라이더, 마우스 감도 슬라이더,
        /// 창 크기 선택 버튼, 뒤로 버튼이 포함된다.
        /// 일시정지 중 열린 설정이면 게임 화면 위에 반투명 오버레이로 표시된다.
        /// </summary>
        /// <param name="r">드로우 명령을 받을 렌더러 인스턴스.</param>
        /// <param name="width">클라이언트 영역 너비.</param>
        /// <param name="height">클라이언트 영역 높이.</param>
        /// <param name="overlay">
        /// true이면 반투명 배경 오버레이로 그리고,
        /// false이면 불투명 단색 배경으로 그린다.
        /// </param>
        private void DrawSettings(Renderer r, int width, int height, bool overlay)
        {
            WorldSettingsSnapshot settings = GetDisplayedSettings();

            if (overlay)
            {
                r.DrawRectangle(0, 0, width, height, Color.FromArgb(160, 0, 0, 0));
            }
            else
            {
                r.DrawRectangle(0, 0, width, height, Color.FromArgb(25, 25, 25));
            }

            int panelW = Math.Min(620, Math.Max(420, width - 90));
            int panelH = 420;
            int panelX = (width - panelW) / 2;
            int panelY = (height - panelH) / 2;
            r.DrawRectangle(panelX, panelY, panelW, panelH, Color.FromArgb(220, 30, 30, 30));

            r.DrawTextCenteredShadow("설정", width * 0.5f, panelY + 38, Color.White, 24f);

            float fovValue = settings.FovDegrees;
            float sensValue = settings.MouseSensitivity;

            // 시야각 슬라이더
            r.DrawText("시야각 (60 - 90)", fovSliderRect.X, fovSliderRect.Y - 32, Color.White, 14f);
            DrawSlider(r, fovSliderRect, GetRatio(fovValue, FovMin, FovMax));
            r.DrawText(fovValue.ToString("0"), fovSliderRect.Right + 14, fovSliderRect.Y - 8, Color.White, 14f);

            // 감도변경 슬라이더
            r.DrawText("마우스 감도", sensSliderRect.X, sensSliderRect.Y - 32, Color.White, 14f);
            DrawSlider(r, sensSliderRect, GetRatio(sensValue, SensMin, SensMax));
            r.DrawText(sensValue.ToString("0.000"), sensSliderRect.Right + 14, sensSliderRect.Y - 8, Color.White, 14f);

            // 창크기 변경 박스: 960, 1270, 1600, 1920
            r.DrawText("창 크기 (16:9 고정)", resolutionPrevRect.X, resolutionPrevRect.Y - 34, Color.White, 14f);
            DrawMenuButton(r, resolutionPrevRect, "<", resolutionPrevRect.Contains(lastMousePosition) ? Color.FromArgb(225, 74, 74, 74) : Color.FromArgb(210, 42, 42, 42));
            DrawMenuButton(r, resolutionNextRect, ">", resolutionNextRect.Contains(lastMousePosition) ? Color.FromArgb(225, 74, 74, 74) : Color.FromArgb(210, 42, 42, 42));
            r.DrawRectangle(resolutionPrevRect.Right + 14, resolutionPrevRect.Y, resolutionNextRect.X - resolutionPrevRect.Right - 28, resolutionPrevRect.Height, Color.FromArgb(210, 38, 38, 38));
            r.DrawTextCentered(GetCurrentWindowSizeLabel(), width * 0.5f, resolutionPrevRect.Y + resolutionPrevRect.Height * 0.5f, Color.White, 18f);

            // BGM 볼륨 슬라이더: 0~100 범위를 0.0~1.0 비율로 변환하여 표시한다.
            int bgmVolume = settings.BgmVolume;
            r.DrawText("BGM 볼륨", bgmSliderRect.X, bgmSliderRect.Y - 32, Color.White, 14f);
            DrawSlider(r, bgmSliderRect, bgmVolume / 100f);
            r.DrawText(bgmVolume.ToString(), bgmSliderRect.Right + 14, bgmSliderRect.Y - 8, Color.White, 14f);

            // 효과음 볼륨 슬라이더: 0~100 범위를 0.0~1.0 비율로 변환하여 표시한다.
            int sfxVolume = settings.SfxVolume;
            r.DrawText("효과음 볼륨", sfxSliderRect.X, sfxSliderRect.Y - 32, Color.White, 14f);
            DrawSlider(r, sfxSliderRect, sfxVolume / 100f);
            r.DrawText(sfxVolume.ToString(), sfxSliderRect.Right + 14, sfxSliderRect.Y - 8, Color.White, 14f);

            DrawMenuButton(r, backButtonRect, "뒤로");
        }

        /// <summary>
        /// 메뉴 화면의 배경 장식을 그린다.
        /// 상단/하단 색띠, 수평/수직 구분선 등 전술적 분위기의 기하학적 요소들로 구성된다.
        /// </summary>
        /// <param name="r">드로우 명령을 받을 렌더러 인스턴스.</param>
        /// <param name="width">클라이언트 영역 너비.</param>
        /// <param name="height">클라이언트 영역 높이.</param>
        private void DrawMenuBackdrop(Renderer r, int width, int height)
        {
            r.DrawRectangle(0, 0, width, height, Color.FromArgb(18, 18, 18));
            r.DrawRectangle(0, 0, width, height * 0.22f, Color.FromArgb(255, 32, 18, 18));
            r.DrawRectangle(0, height * 0.78f, width, height * 0.22f, Color.FromArgb(255, 20, 20, 20));
            r.DrawRectangle(width * 0.08f, height * 0.12f, width * 0.84f, 3, Color.FromArgb(180, 120, 28, 28));
            r.DrawRectangle(width * 0.16f, height * 0.84f, width * 0.68f, 2, Color.FromArgb(120, 180, 180, 180));
            r.DrawRectangle(width * 0.1f, height * 0.16f, 6, height * 0.62f, Color.FromArgb(70, 120, 28, 28));
            r.DrawRectangle(width * 0.9f, height * 0.16f, 6, height * 0.62f, Color.FromArgb(70, 120, 28, 28));
        }

        /// <summary>
        /// 제목과 부제목을 가진 액션 버튼을 그린다.
        /// hover 상태이면 강조 테두리, 더 밝은 패널 색, 더 큰 텍스트로 표시된다.
        /// 왼쪽에 accent 색띠가 세로로 그려진다.
        /// </summary>
        /// <param name="r">드로우 명령을 받을 렌더러 인스턴스.</param>
        /// <param name="rect">버튼이 차지할 화면 영역.</param>
        /// <param name="title">버튼의 주 제목 텍스트 (예: "게임 시작").</param>
        /// <param name="subtitle">버튼의 부제목 텍스트 (예: "모드 선택 후 전장 진입").</param>
        /// <param name="accentColor">왼쪽 색띠와 hover 테두리에 사용할 강조 색.</param>
        /// <param name="highlighted">마우스 커서가 버튼 위에 있을 때 true.</param>
        private void DrawMenuActionButton(Renderer r, Rectangle rect, string title, string subtitle, Color accentColor, bool highlighted)
        {
            int border = highlighted ? 4 : 0;
            if (highlighted)
            {
                r.DrawRectangle(rect.X - border, rect.Y - border, rect.Width + border * 2, rect.Height + border * 2, Color.FromArgb(95, accentColor.R, accentColor.G, accentColor.B));
            }

            Color panelColor = highlighted ? Color.FromArgb(238, 48, 48, 48) : Color.FromArgb(225, 32, 32, 32);
            int accentWidth = highlighted ? 10 : 8;
            float titleSize = highlighted ? 18f : 17f;
            float subtitleSize = highlighted ? 11f : 10.5f;

            r.DrawRectangle(rect.X, rect.Y, rect.Width, rect.Height, panelColor);
            r.DrawRectangle(rect.X + 8, rect.Y + 8, accentWidth, rect.Height - 16, accentColor);
            r.DrawRectangle(rect.X + 24, rect.Y + 8, rect.Width - 32, 1, Color.FromArgb(highlighted ? 110 : 70, 255, 255, 255));
            r.DrawText(title, rect.X + 36, rect.Y + 12, Color.White, titleSize);
            r.DrawText(subtitle, rect.X + 36, rect.Y + 38, Color.FromArgb(220, 210, 210, 210), subtitleSize);
        }

        /// <summary>
        /// 기본 어두운 배경색으로 단순 버튼을 그린다.
        /// </summary>
        /// <param name="r">드로우 명령을 받을 렌더러 인스턴스.</param>
        /// <param name="rect">버튼이 차지할 화면 영역.</param>
        /// <param name="text">버튼 중앙에 표시할 텍스트.</param>
        private void DrawMenuButton(Renderer r, Rectangle rect, string text)
        {
            DrawMenuButton(r, rect, text, Color.FromArgb(200, 40, 40, 40));
        }

        /// <summary>
        /// 지정한 배경색으로 단순 버튼을 그린다. 텍스트는 버튼 중앙에 흰색으로 표시된다.
        /// </summary>
        /// <param name="r">드로우 명령을 받을 렌더러 인스턴스.</param>
        /// <param name="rect">버튼이 차지할 화면 영역.</param>
        /// <param name="text">버튼 중앙에 표시할 텍스트.</param>
        /// <param name="color">버튼 배경색.</param>
        private void DrawMenuButton(Renderer r, Rectangle rect, string text, Color color)
        {
            r.DrawRectangle(rect.X, rect.Y, rect.Width, rect.Height, color);
            r.DrawTextCentered(text, rect.X + rect.Width * 0.5f, rect.Y + rect.Height * 0.5f, Color.White, 19f);
        }

        /// <summary>
        /// 가로 슬라이더를 그린다. 배경 트랙, 채워진 부분, 드래그 손잡이(knob)로 구성된다.
        /// ratio가 0이면 완전히 비어있고, 1이면 완전히 채워진다.
        /// </summary>
        /// <param name="r">드로우 명령을 받을 렌더러 인스턴스.</param>
        /// <param name="rect">슬라이더 트랙이 차지할 화면 영역.</param>
        /// <param name="ratio">현재 값의 비율 (0.0 ~ 1.0). 자동으로 클램핑된다.</param>
        private void DrawSlider(Renderer r, Rectangle rect, float ratio)
        {
            ratio = Clamp01(ratio);
            r.DrawRectangle(rect.X, rect.Y, rect.Width, rect.Height, Color.FromArgb(120, 60, 60, 60));

            int filledW = (int)(rect.Width * ratio);
            if (filledW > 0)
            {
                r.DrawRectangle(rect.X, rect.Y, filledW, rect.Height, Color.FromArgb(200, 90, 170, 90));
            }

            int knobW = 16;
            int knobH = 28;
            int knobX = rect.X + filledW - knobW / 2;
            if (knobX < rect.X - knobW / 2) knobX = rect.X - knobW / 2;
            if (knobX > rect.X + rect.Width - knobW / 2) knobX = rect.X + rect.Width - knobW / 2;
            int knobY = rect.Y - (knobH - rect.Height) / 2;
            r.DrawRectangle(knobX, knobY, knobW, knobH, Color.FromArgb(220, 220, 220, 220));
        }

        /// <summary>
        /// 메인 메뉴 버튼들의 위치와 크기를 현재 창 크기에 맞게 계산한다.
        /// 버튼은 수직 방향으로 일정 간격을 두고 화면 중앙에 정렬된다.
        /// </summary>
        /// <param name="width">클라이언트 영역 너비.</param>
        /// <param name="height">클라이언트 영역 높이.</param>
        private void UpdateMenuLayout(int width, int height)
        {
            int buttonW = Math.Min(360, Math.Max(240, width - 140));
            int buttonH = 68;
            int centerX = width / 2;
            int spacing = 18;

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
            int panelW = Math.Min(580, Math.Max(380, width - 120));
            int panelH = GetModePanelHeight();
            int panelY = (height - panelH) / 2;

            int buttonW = Math.Min(340, panelW - 96);
            int buttonH = 60;
            int spacing = 14;
            int centerX = width / 2;
            int startY = panelY + 126;

            modeNormalRect  = new Rectangle(centerX - buttonW / 2, startY,                          buttonW, buttonH);
            modeEndlessRect = new Rectangle(centerX - buttonW / 2, startY + (buttonH + spacing),     buttonW, buttonH);
            continueButtonRect = new Rectangle(centerX - buttonW / 2, startY + (buttonH + spacing) * 2, buttonW, buttonH);
            modeBackRect    = new Rectangle(centerX - 96, panelY + panelH - 62, 192, 46);
        }

        private int GetModePanelHeight()
        {
            return CanContinueSavedRun ? 490 : 420;
        }

        /// <summary>
        /// 일시정지 화면 버튼들의 위치와 크기를 현재 창 크기에 맞게 계산한다.
        /// 게임 계속/설정/메인메뉴 버튼이 화면 중앙에 수직으로 배치된다.
        /// </summary>
        /// <param name="width">클라이언트 영역 너비.</param>
        /// <param name="height">클라이언트 영역 높이.</param>
        private void UpdatePauseLayout(int width, int height)
        {
            int buttonW = 250;
            int buttonH = 52;
            int centerX = width / 2;
            int startY = height / 2 - 18;
            int spacing = 16;

            pauseResumeRect = new Rectangle(centerX - buttonW / 2, startY, buttonW, buttonH);
            pauseSettingsRect = new Rectangle(centerX - buttonW / 2, startY + buttonH + spacing, buttonW, buttonH);
            pauseMenuRect = new Rectangle(centerX - buttonW / 2, startY + (buttonH + spacing) * 2, buttonW, buttonH);
        }

        private void UpdateDeathLayout(int width, int height)
        {
            int panelW = Math.Min(540, Math.Max(380, width - 120));
            int panelH = 340;
            int panelX = (width - panelW) / 2;
            int panelY = (height - panelH) / 2;
            int buttonH = 50;
            int buttonW = Math.Min(210, (panelW - 78) / 2);
            int buttonY = panelY + panelH - 76;

            deathRestartRect = new Rectangle(panelX + 28, buttonY, buttonW, buttonH);
            deathMenuRect = new Rectangle(panelX + panelW - 28 - buttonW, buttonY, buttonW, buttonH);
        }

        /// <summary>
        /// 설정 화면 컨트롤들의 위치와 크기를 현재 창 크기에 맞게 계산한다.
        /// FOV 슬라이더, 감도 슬라이더, 창 크기 이전/다음 버튼, 뒤로 버튼의 영역을 설정한다.
        /// </summary>
        /// <param name="width">클라이언트 영역 너비.</param>
        /// <param name="height">클라이언트 영역 높이.</param>
        private void UpdateSettingsLayout(int width, int height)
        {
            int panelW = Math.Min(620, Math.Max(420, width - 90));
            int panelH = 590;
            int panelX = (width - panelW) / 2;
            int panelY = (height - panelH) / 2;

            int sliderW = panelW - 180;
            int sliderH = 12;
            int sliderX = panelX + 36;
            int firstY = panelY + 112;
            int spacing = 84;

            fovSliderRect = new Rectangle(sliderX, firstY, sliderW, sliderH);
            sensSliderRect = new Rectangle(sliderX, firstY + spacing, sliderW, sliderH);

            int resolutionY = sensSliderRect.Y + spacing;
            int resolutionButtonW = 54;
            int resolutionButtonH = 42;
            resolutionPrevRect = new Rectangle(panelX + 36, resolutionY, resolutionButtonW, resolutionButtonH);
            resolutionNextRect = new Rectangle(panelX + panelW - 36 - resolutionButtonW, resolutionY, resolutionButtonW, resolutionButtonH);

            int bgmY = resolutionY + resolutionButtonH + 42;
            bgmSliderRect = new Rectangle(sliderX, bgmY, sliderW, sliderH);
            sfxSliderRect = new Rectangle(sliderX, bgmY + spacing, sliderW, sliderH);

            backButtonRect = new Rectangle(panelX + panelW / 2 - 96, panelY + panelH - 72, 192, 46);
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
            DrawMenuBackdrop(r, width, height);

            float cx = width * 0.5f;
            float panelW = Math.Min(740f, (float)(width - 80));
            float panelH = Math.Min(520f, (float)(height - 80));
            float panelX = cx - panelW * 0.5f;
            float panelY = (height - panelH) * 0.5f;

            r.DrawRectangle(panelX, panelY, panelW, panelH, Color.FromArgb(228, 14, 18, 28));
            r.DrawRectangle(panelX, panelY, panelW, 1f, Color.FromArgb(200, 100, 140, 200));
            r.DrawRectangle(panelX, panelY + panelH - 1f, panelW, 1f, Color.FromArgb(200, 100, 140, 200));
            r.DrawRectangle(panelX, panelY, 1f, panelH, Color.FromArgb(200, 100, 140, 200));
            r.DrawRectangle(panelX + panelW - 1f, panelY, 1f, panelH, Color.FromArgb(200, 100, 140, 200));

            r.DrawTextCenteredShadow("[ 기록 ]", cx, panelY + 20f, Color.FromArgb(255, 180, 210, 255), 16f);

            WorldRunRecordSnapshot[] records = LoadWorldRunRecords(15);

            if (records.Length == 0)
            {
                r.DrawTextCenteredShadow("아직 도전 기록이 없습니다.", cx, panelY + panelH * 0.5f, Color.FromArgb(200, 180, 180, 180), 13f);
            }
            else
            {
                r.DrawTextCenteredShadow($"총 도전 횟수: {records.Length}회", cx, panelY + 48f, Color.FromArgb(220, 200, 200, 200), 10f);

                float headerY = panelY + 76f;
                float colFloor  = panelX + panelW * 0.12f;
                float colEnemy  = panelX + panelW * 0.30f;
                float colBoss   = panelX + panelW * 0.48f;
                float colTime   = panelX + panelW * 0.65f;
                float colDate   = panelX + panelW * 0.84f;
                Color headerCol = Color.FromArgb(255, 180, 210, 255);
                Color dimLine   = Color.FromArgb(80, 180, 210, 255);

                r.DrawText("도달 층", colFloor, headerY, headerCol, 9f);
                r.DrawText("적 처치", colEnemy, headerY, headerCol, 9f);
                r.DrawText("보스 처치", colBoss, headerY, headerCol, 9f);
                r.DrawText("플레이 시간", colTime, headerY, headerCol, 9f);
                r.DrawText("날짜", colDate, headerY, headerCol, 9f);

                float rowH = 28f;
                float rowStart = headerY + 22f;
                for (int i = 0; i < records.Length; i++)
                {
                    var rec = records[i];
                    float rowY = rowStart + i * rowH;
                    if (rowY + rowH > panelY + panelH - 58f) break;

                    if (i % 2 == 0)
                        r.DrawRectangle(panelX + 2f, rowY - 4f, panelW - 4f, rowH, Color.FromArgb(40, 0, 0, 0));

                    r.DrawRectangle(panelX + 2f, rowY + rowH - 5f, panelW - 4f, 1f, dimLine);

                    Color rowCol = i == 0 ? Color.FromArgb(255, 255, 200, 60) : Color.FromArgb(255, 220, 220, 220);
                    string timeStr = FormatDuration(rec.DurationSeconds);
                    r.DrawText($"{rec.FloorReached}층", colFloor, rowY, rowCol, 9.5f);
                    r.DrawText($"{rec.EnemiesKilled}명", colEnemy, rowY, rowCol, 9.5f);
                    r.DrawText($"{rec.BossesKilled}명", colBoss, rowY, rowCol, 9.5f);
                    r.DrawText(timeStr, colTime, rowY, rowCol, 9.5f);
                    r.DrawText(rec.EndedAt, colDate, rowY, rowCol, 9f);
                }
            }

            int backW = 180, backH = 44;
            recordsBackRect = new Rectangle((int)(cx - backW * 0.5f), (int)(panelY + panelH - 58f), backW, backH);
            Color backColor = recordsBackRect.Contains(lastMousePosition)
                ? Color.FromArgb(220, 60, 80, 110)
                : Color.FromArgb(200, 36, 50, 72);
            DrawMenuButton(r, recordsBackRect, "← 뒤로", backColor);

            r.DrawTextCenteredShadow("ESC 또는 클릭으로 뒤로", cx, panelY + panelH - 10f, Color.FromArgb(150, 180, 180, 180), 8f);
        }

        private void HandleRecordsClick(Point location)
        {
            if (recordsBackRect.Contains(location))
                EnterMenuState(GameState.Menu);
        }

        private static string FormatDuration(int seconds)
        {
            int m = seconds / 60;
            int s = seconds % 60;
            return m > 0 ? $"{m}분 {s:00}초" : $"{s}초";
        }
    }
}
