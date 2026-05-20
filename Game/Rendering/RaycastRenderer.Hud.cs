using System;
using System.Drawing;
using My2DEngine.Engine.Rendering;
using My2DEngine.Game.Config;
using My2DEngine.Game.Map;
using My2DEngine.Game.Systems;
using My2DEngine.Game;

namespace My2DEngine.Game.Rendering
{
    /// <summary>
    /// 체력 바, 스태미나 바, 탄약 카운터, 미니맵, 보스 HUD, 무기 오버레이,
    /// 피격 오버레이, 사망 연출 등 모든 화면 HUD와 오버레이를 담당하는 partial 클래스.
    /// 현재 HUD는 Renderer 기반 GPU/백엔드 호출만 사용한다.
    /// </summary>
    public partial class RaycastRenderer
    {
        /// <summary>미니맵에서 플레이어 중심 기준으로 표시할 타일 반경(타일 단위).</summary>
        private const int MiniMapTileRadius = 5;

        /// <summary>미니맵에서 타일 하나를 표시하는 픽셀 크기.</summary>
        private const int MiniMapCellSize = 11;

        /// <summary>
        /// 보스 이름 텍스트를 렌더러를 통해 화면에 그린다.
        /// 보스가 없거나 사망했으면 아무것도 그리지 않는다.
        /// </summary>
        /// <param name="r">텍스트를 그릴 렌더러.</param>
        /// <param name="bossEnemy">보스 적 인스턴스. null이거나 사망 상태이면 무시된다.</param>
        private void DrawBossHud(Renderer r, Enemy bossEnemy)
        {
            if (bossEnemy == null || !bossEnemy.Alive)
            {
                return;
            }

            float panelY = 14f;
            r.DrawTextCenteredShadow(string.IsNullOrWhiteSpace(bossEnemy.DisplayName) ? "Boss" : bossEnemy.DisplayName,
                frameW * 0.5f, panelY + 7f, Color.FromArgb(255, 255, 222, 190), 12f);
        }

        /// <summary>
        /// 스테이지 상태 메시지, 상호작용 힌트 텍스트, 보스 등장 안내, 스테이지 클리어 문구를
        /// 렌더러를 통해 화면에 그린다.
        /// </summary>
        /// <param name="r">텍스트를 그릴 렌더러.</param>
        /// <param name="stageStatusMessage">화면 상단에 표시할 스테이지 상태 메시지.</param>
        /// <param name="interactPromptText">화면 하단에 표시할 상호작용 힌트 텍스트.</param>
        /// <param name="bossIntroTimer">보스 등장 연출 남은 시간(초). 0 이하이면 생략한다.</param>
        /// <param name="bossEnemy">보스 적 인스턴스. 이름 표시에 사용된다.</param>
        /// <param name="victory">스테이지 클리어 여부.</param>
        private void DrawStageOverlay(Renderer r, string stageStatusMessage, string interactPromptText, float bossIntroTimer, Enemy bossEnemy, bool victory)
        {
            if (!string.IsNullOrWhiteSpace(stageStatusMessage))
            {
                float panelH = 28f;
                float panelY = frameH * 0.06f;
                r.DrawTextCenteredShadow(stageStatusMessage, frameW * 0.5f, panelY + panelH * 0.5f,
                    Color.FromArgb(255, 235, 220, 180), 11f);
            }

            if (!string.IsNullOrWhiteSpace(interactPromptText))
            {
                float panelH = 26f;
                float panelY = frameH * 0.80f;
                r.DrawTextCenteredShadow(interactPromptText, frameW * 0.5f, panelY + panelH * 0.5f,
                    Color.FromArgb(255, 220, 220, 220), 11f);
            }

            if (bossIntroTimer > 0f && bossEnemy != null && bossEnemy.Alive)
            {
                r.DrawTextCenteredShadow("BOSS ENCOUNTER", frameW * 0.5f, frameH * 0.29f,
                    Color.FromArgb(255, 255, 145, 95), 20f);
                r.DrawTextCenteredShadow(string.IsNullOrWhiteSpace(bossEnemy.DisplayName) ? "Arena Warden" : bossEnemy.DisplayName,
                    frameW * 0.5f, frameH * 0.335f, Color.White, 14f);
            }

            if (victory)
            {
                r.DrawTextCenteredShadow("STAGE CLEAR", frameW * 0.5f, frameH * 0.42f,
                    Color.FromArgb(255, 255, 220, 130), 24f);
                r.DrawTextCenteredShadow("Boss eliminated", frameW * 0.5f, frameH * 0.47f, Color.White, 12f);
            }
        }

        /// <summary>
        /// "YOU DIED" 텍스트와 재시작 안내 텍스트를 렌더러로 그린다.
        /// deathProgress가 0.2 미만이면 텍스트가 표시되지 않는다.
        /// </summary>
        /// <param name="r">텍스트를 그릴 렌더러.</param>
        /// <param name="deathProgress">사망 연출 진행 비율(0=시작, 1=완료).</param>
        private void DrawDeathPresentation(Renderer r, float deathProgress)
        {
            float uiFade = deathProgress <= 0.2f ? 0f : (deathProgress - 0.2f) / 0.8f;
            uiFade = EaseOutCubic(uiFade);
            if (uiFade <= 0f)
            {
                return;
            }

            float panelY = frameH * 0.39f;
            r.DrawTextCenteredShadow("YOU DIED", frameW * 0.5f, panelY + 42f,
                Color.FromArgb((int)(255f * uiFade), 255, 218, 218), 30f);
            r.DrawTextCenteredShadow("Run terminated", frameW * 0.5f, panelY + 88f,
                Color.FromArgb((int)(235f * uiFade), 255, 245, 245), 14f);
        }

        /// <summary>
        /// 피격 방향에 따라 화면 흔들림 오프셋을 계산한다.
        /// 피격 방향 벡터를 플레이어 시야 좌표계로 분해한 뒤 사인/코사인 진동을 더해
        /// 방향성 있는 충격감 있는 흔들림을 생성한다.
        /// </summary>
        /// <param name="player">시야 방향 계산 기준이 되는 플레이어 상태.</param>
        /// <param name="shakeTimer">피격 흔들림 남은 시간(초). 0 이하이면 오프셋이 0이다.</param>
        /// <param name="shakePower">흔들림 세기(0~1).</param>
        /// <param name="damageDirX">피격 방향 벡터의 X 성분(월드 공간).</param>
        /// <param name="damageDirY">피격 방향 벡터의 Y 성분(월드 공간).</param>
        /// <param name="offsetX">계산된 수평 화면 오프셋(픽셀).</param>
        /// <param name="offsetY">계산된 수직 화면 오프셋(픽셀).</param>
        private void GetDamageShakeOffset(Player player, float shakeTimer, float shakePower, float damageDirX, float damageDirY,
            out float offsetX, out float offsetY)
        {
            offsetX = 0f;
            offsetY = 0f;

            if (shakeTimer <= 0f || shakePower <= 0f)
            {
                return;
            }

            float dirLen = (float)Math.Sqrt((damageDirX * damageDirX) + (damageDirY * damageDirY));
            if (dirLen <= 0.001f)
            {
                damageDirX = -player.Direction.X;
                damageDirY = -player.Direction.Y;
                dirLen = 1f;
            }

            damageDirX /= dirLen;
            damageDirY /= dirLen;

            float rightX = -player.Direction.Y;
            float rightY = player.Direction.X;
            float dirForward = (damageDirX * player.Direction.X) + (damageDirY * player.Direction.Y);
            float dirRight = (damageDirX * rightX) + (damageDirY * rightY);

            float amplitude = (3f + 7f * shakePower) * Math.Min(1f, shakeTimer / 0.28f);
            float oscillationX = (float)Math.Sin(shakeTimer * 75f);
            float oscillationY = (float)Math.Cos(shakeTimer * 62f);

            offsetX = ((oscillationX * 0.55f) - (dirRight * 0.95f)) * amplitude;
            offsetY = ((oscillationY * 0.4f) - (dirForward * 0.7f)) * amplitude;
        }

        /// <summary>
        /// 무기 발사 반동에 따른 화면 흔들림 오프셋을 계산한다.
        /// 수직 킥과 가로 진동을 합산하여 사실적인 총기 반동 느낌을 연출한다.
        /// </summary>
        /// <param name="shakeTimer">반동 흔들림 남은 시간(초). 0 이하이면 오프셋이 0이다.</param>
        /// <param name="shakePower">반동 세기(0~1).</param>
        /// <param name="offsetX">계산된 수평 화면 오프셋(픽셀).</param>
        /// <param name="offsetY">계산된 수직 화면 오프셋(픽셀).</param>
        private void GetRecoilShakeOffset(float shakeTimer, float shakePower, out float offsetX, out float offsetY)
        {
            offsetX = 0f;
            offsetY = 0f;

            if (shakeTimer <= 0f || shakePower <= 0f)
            {
                return;
            }

            float intensity = Math.Min(1f, shakeTimer / 0.18f) * shakePower;
            float lateral = (float)Math.Sin(shakeTimer * 108f) * (2.2f + shakePower * 2.6f);
            float verticalKick = (5f + shakePower * 8f) * intensity;
            float settle = (float)Math.Cos(shakeTimer * 56f) * 1.4f * intensity;

            offsetX = lateral * intensity;
            offsetY = -verticalKick + settle;
        }

        /// <summary>
        /// 탄약 카운터 패널 텍스트를 렌더러로 그린다.
        /// </summary>
        /// <param name="r">텍스트를 그릴 렌더러.</param>
        /// <param name="weapon">탄약 및 특수기 상태를 제공하는 무기 인스턴스.</param>
        private void DrawAmmoCounter(Renderer r, Weapon weapon)
        {
            if (r == null || weapon == null)
            {
                return;
            }

            int panelW = 150;
            int panelH = 132;
            int panelX = frameW - panelW - 16;
            int panelY = frameH - panelH - 16;
            float centerX = panelX + (panelW * 0.5f);

            r.DrawTextCenteredShadow(GetHudWeaponName(weapon.CurrentType), centerX, panelY + 24f,
                Color.FromArgb(210, 220, 220, 220), 10.5f);
            r.DrawTextCenteredShadow(BuildAmmoCounterText(weapon), centerX, panelY + 55f,
                Color.FromArgb(255, 245, 245, 245), 20f);

            r.DrawTextCenteredShadow("AMMO", centerX, panelY + 80f,
                Color.FromArgb(180, 200, 200, 200), 9f);

            string specialStatus = GetSpecialStatusText(weapon);
            if (!string.IsNullOrWhiteSpace(specialStatus))
            {
                r.DrawTextCenteredShadow(specialStatus, centerX, panelY + 98f,
                    Color.FromArgb(220, 255, 175, 95), 9f);
            }
        }

        private string BuildAmmoCounterText(Weapon weapon)
        {
            if (weapon == null)
            {
                return string.Empty;
            }

            return weapon.CurrentAmmo.ToString("00") + "/" + weapon.MagazineSize.ToString("00");
        }

        private string GetHudWeaponName(WeaponType type)
        {
            return WeaponPresentation.GetHudName(type);
        }

        private string GetSpecialStatusText(Weapon weapon)
        {
            if (weapon == null || !weapon.HasSpecialUpgrade(weapon.CurrentType))
            {
                return null;
            }

            if ((weapon.CurrentType == WeaponType.HChainGun || weapon.CurrentType == WeaponType.DuelBerettas) &&
                weapon.SpecialUsedThisFloor)
            {
                return "F USED";
            }

            if (weapon.SpecialCooldownTimer > 0f)
            {
                return "F " + weapon.SpecialCooldownTimer.ToString("0.0") + "s";
            }

            return "F READY";
        }

        /// <summary>
        /// 보유 코인 수를 렌더러로 그린다.
        /// </summary>
        /// <param name="r">텍스트를 그릴 렌더러.</param>
        /// <param name="player">코인 상태를 제공하는 플레이어 상태.</param>
        private void DrawCoinHud(Renderer r, Player player)
        {
            int panelW = 118;
            int panelX = frameW - panelW - 20;
            int panelY = frameH - 220;
            r.DrawText("COIN", panelX + 12, panelY + 11, Color.FromArgb(220, 255, 220, 150), 10f);
            r.DrawText("x" + player.CoinCount, panelX + 56, panelY + 9, Color.White, 16f);
        }

        /// <summary>
        /// 타일 타입 값에 따라 미니맵에 표시할 색상을 반환한다.
        /// 빈 공간, 문, 특수 타일, 벽 타일이 각각 다른 색상으로 구분된다.
        /// </summary>
        /// <param name="tileType">맵 배열의 타일 타입 값.</param>
        /// <returns>미니맵에 표시할 ARGB 색상.</returns>
        private Color GetMiniMapTileColor(int tileType)
        {
            switch (tileType)
            {
                case 0:
                    return Color.FromArgb(105, 46, 46, 46);
                case GameConfig.DoorTileType:
                    return Color.FromArgb(220, 190, 150, 60);
                case 6:
                    return Color.FromArgb(205, 74, 132, 78);
                case 7:
                    return Color.FromArgb(205, 164, 108, 52);
                case 8:
                    return Color.FromArgb(205, 74, 108, 156);
                default:
                    return CollisionSystem.IsSolidType(tileType)
                        ? Color.FromArgb(210, 198, 198, 198)
                        : Color.FromArgb(90, 36, 36, 36);
            }
        }

        /// <summary>
        /// GPU 경로에서 보스 체력 바와 배경 패널을 렌더러를 통해 그린다.
        /// 보스가 없거나 사망했으면 아무것도 그리지 않는다.
        /// </summary>
        /// <param name="r">사각형을 그릴 렌더러.</param>
        /// <param name="bossEnemy">보스 적 인스턴스. null이거나 사망 상태이면 무시된다.</param>
        private void DrawBossHudBackdrop(Renderer r, Enemy bossEnemy)
        {
            if (r == null || bossEnemy == null || !bossEnemy.Alive)
            {
                return;
            }

            float ratio = bossEnemy.MaxHealth > 0f ? bossEnemy.Health / bossEnemy.MaxHealth : 0f;
            if (ratio < 0f) ratio = 0f;
            if (ratio > 1f) ratio = 1f;

            float panelW = Math.Min(frameW * 0.39f, 352f);
            float panelH = 31f;
            float panelX = (frameW - panelW) * 0.5f;
            float panelY = 14f;
            r.DrawRectangle(panelX, panelY, panelW, panelH, Color.FromArgb(165, 12, 12, 12));
            r.DrawRectangle(panelX + 9f, panelY + 16f, panelW - 18f, 8f, Color.FromArgb(185, 55, 24, 20));
            r.DrawRectangle(panelX + 9f, panelY + 16f, (panelW - 18f) * ratio, 8f, Color.FromArgb(220, 225, 95, 55));
        }

        /// <summary>
        /// GPU 경로에서 스테이지 상태 메시지, 상호작용 힌트, 보스 등장 오버레이,
        /// 클리어 패널 배경을 렌더러를 통해 그린다.
        /// </summary>
        /// <param name="r">사각형을 그릴 렌더러. null이면 아무것도 그리지 않는다.</param>
        /// <param name="stageStatusMessage">상단에 표시할 스테이지 상태 메시지.</param>
        /// <param name="interactPromptText">하단에 표시할 상호작용 힌트 텍스트.</param>
        /// <param name="bossIntroTimer">보스 등장 연출 남은 시간(초).</param>
        /// <param name="bossEnemy">보스 적 인스턴스.</param>
        /// <param name="victory">스테이지 클리어 여부.</param>
        private void DrawStageBackdrop(Renderer r, string stageStatusMessage, string interactPromptText, float bossIntroTimer, Enemy bossEnemy, bool victory)
        {
            if (r == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(stageStatusMessage))
            {
                float panelW = Math.Min(frameW * 0.72f, Math.Max(180f, stageStatusMessage.Length * 7.4f));
                float panelH = 28f;
                float panelX = (frameW - panelW) * 0.5f;
                float panelY = frameH * 0.06f;
                r.DrawRectangle(panelX, panelY, panelW, panelH, Color.FromArgb(150, 10, 10, 10));
            }

            if (!string.IsNullOrWhiteSpace(interactPromptText))
            {
                float panelW = Math.Min(frameW * 0.72f, Math.Max(150f, interactPromptText.Length * 7.2f));
                float panelH = 26f;
                float panelX = (frameW - panelW) * 0.5f;
                float panelY = frameH * 0.80f;
                r.DrawRectangle(panelX, panelY, panelW, panelH, Color.FromArgb(150, 10, 10, 10));
            }

            if (bossIntroTimer > 0f && bossEnemy != null && bossEnemy.Alive)
            {
                float alpha = Math.Min(1f, bossIntroTimer / GameConfig.BossIntroDuration);
                int overlayAlpha = (int)(110f * alpha);
                r.DrawRectangle(0f, frameH * 0.24f, frameW, 96f, Color.FromArgb(overlayAlpha, 0, 0, 0));
            }

            if (victory)
            {
                r.DrawRectangle(frameW * 0.5f - 180f, frameH * 0.38f, 360f, 84f, Color.FromArgb(160, 0, 0, 0));
            }
        }

        /// <summary>
        /// GPU 경로에서 미니맵 전체(배경, 타일, 적 점, 플레이어 점, 시야 방향)를
        /// 렌더러를 통해 그린다.
        /// </summary>
        /// <param name="r">사각형을 그릴 렌더러. null이면 아무것도 그리지 않는다.</param>
        /// <param name="player">위치 및 시야 방향을 제공하는 플레이어 상태.</param>
        private void DrawMiniMap(Renderer r, Player player)
        {
            if (r == null || player == null)
            {
                return;
            }

            int[,] map = mapManager.Map;
            if (map == null)
            {
                return;
            }

            const float cellSize = MiniMapCellSize;
            const float padding = 12f;
            float visibleTiles = MiniMapTileRadius * 2 + 1;
            float panelSize = visibleTiles * cellSize;
            float panelX = padding;
            float panelY = padding;
            float centerPixelX = panelX + (panelSize * 0.5f);
            float centerPixelY = panelY + (panelSize * 0.5f);
            int playerTileX = (int)Math.Floor(player.Position.X);
            int playerTileY = (int)Math.Floor(player.Position.Y);
            int mapWidth = map.GetLength(0);
            int mapHeight = map.GetLength(1);
            float subTileOffsetX = (player.Position.X - playerTileX) * cellSize;
            float subTileOffsetY = (player.Position.Y - playerTileY) * cellSize;

            r.DrawRectangle(panelX - 5f, panelY - 5f, panelSize + 10f, panelSize + 10f, Color.FromArgb(125, 0, 0, 0));
            r.DrawRectangle(panelX, panelY, panelSize, panelSize, Color.FromArgb(170, 16, 16, 16));

            for (int localY = -MiniMapTileRadius; localY <= MiniMapTileRadius; localY++)
            {
                for (int localX = -MiniMapTileRadius; localX <= MiniMapTileRadius; localX++)
                {
                    int mapX = playerTileX + localX;
                    int mapY = playerTileY + localY;
                    Color cellColor = Color.FromArgb(90, 12, 12, 12);
                    if (mapX >= 0 && mapY >= 0 && mapX < mapWidth && mapY < mapHeight)
                    {
                        cellColor = GetMiniMapTileColor(map[mapX, mapY]);
                    }

                    float drawX = panelX + ((localX + MiniMapTileRadius) * cellSize) - subTileOffsetX;
                    float drawY = panelY + ((localY + MiniMapTileRadius) * cellSize) - subTileOffsetY;
                    r.DrawRectangle(drawX, drawY, cellSize + 1f, cellSize + 1f, cellColor);
                }
            }

            Enemy[] enemies = enemyManager.Enemies;
            if (enemies != null)
            {
                foreach (Enemy enemy in enemies)
                {
                    if (enemy == null || !enemy.Alive)
                    {
                        continue;
                    }

                    float relX = (enemy.X - player.Position.X) * cellSize;
                    float relY = (enemy.Y - player.Position.Y) * cellSize;
                    if (Math.Abs(relX) > panelSize * 0.5f + cellSize || Math.Abs(relY) > panelSize * 0.5f + cellSize)
                    {
                        continue;
                    }

                    float enemyX = centerPixelX + relX;
                    float enemyY = centerPixelY + relY;
                    Color enemyColor = enemy.IsBoss
                        ? Color.Gold
                        : enemy.IsMiniBoss
                            ? Color.DeepSkyBlue
                            : Color.OrangeRed;
                    r.DrawRectangle(enemyX - 3f, enemyY - 3f, 6f, 6f, enemyColor);
                }
            }

            r.DrawRectangle(centerPixelX - 4f, centerPixelY - 4f, 8f, 8f, Color.Gold);
            for (int i = 1; i <= 10; i++)
            {
                float t = i / 10f;
                float dirX = centerPixelX + (player.Direction.X * t * 26f);
                float dirY = centerPixelY + (player.Direction.Y * t * 26f);
                r.DrawRectangle(dirX - 1.2f, dirY - 1.2f, 4f, 4f, Color.Gold);
            }
        }

        /// <summary>
        /// GPU 경로에서 탄약 카운터 패널 배경을 렌더러를 통해 그린다.
        /// </summary>
        /// <param name="r">사각형을 그릴 렌더러. null이면 아무것도 그리지 않는다.</param>
        /// <param name="weapon">탄약 및 특수기 상태를 제공하는 무기 인스턴스.</param>
        private void DrawAmmoCounterBackdrop(Renderer r, Weapon weapon)
        {
            if (r == null)
            {
                return;
            }

            int panelW = 150;
            int panelH = 132;
            int panelX = frameW - panelW - 16;
            int panelY = frameH - panelH - 16;
            r.DrawRectangle(panelX, panelY, panelW, panelH, Color.FromArgb(190, 38, 38, 38));
        }

        /// <summary>
        /// GPU 경로에서 코인 HUD 패널 배경을 렌더러를 통해 그린다.
        /// </summary>
        /// <param name="r">사각형을 그릴 렌더러. null이면 아무것도 그리지 않는다.</param>
        private void DrawCoinHudBackdrop(Renderer r)
        {
            if (r == null)
            {
                return;
            }

            int panelW = 118;
            int panelH = 42;
            int panelX = frameW - panelW - 20;
            int panelY = frameH - 220;
            r.DrawRectangle(panelX, panelY, panelW, panelH, Color.FromArgb(185, 42, 36, 18));
        }

        /// <summary>
        /// GPU 경로에서 피격 방향에 따라 화면 가장자리에 붉은 번쩍임 오버레이를
        /// 렌더러를 통해 그린다. 피격 방향에 해당하는 가장자리가 더 밝게 표시된다.
        /// </summary>
        /// <param name="r">사각형을 그릴 렌더러. null이면 아무것도 그리지 않는다.</param>
        /// <param name="player">시야 방향 계산에 사용하는 플레이어 상태.</param>
        /// <param name="damageFlash">번쩍임 강도(0~1). 0이면 아무것도 그리지 않는다.</param>
        /// <param name="damageDirX">피격 방향 벡터의 X 성분(월드 공간).</param>
        /// <param name="damageDirY">피격 방향 벡터의 Y 성분(월드 공간).</param>
        private void DrawPlayerDamageOverlay(Renderer r, Player player, float damageFlash, float damageDirX, float damageDirY)
        {
            if (r == null || damageFlash <= 0f)
            {
                return;
            }

            if (damageFlash > 1f)
            {
                damageFlash = 1f;
            }

            float dirLen = (float)Math.Sqrt((damageDirX * damageDirX) + (damageDirY * damageDirY));
            if (dirLen <= 0.001f)
            {
                damageDirX = -player.Direction.X;
                damageDirY = -player.Direction.Y;
                dirLen = 1f;
            }

            damageDirX /= dirLen;
            damageDirY /= dirLen;

            float rightX = -player.Direction.Y;
            float rightY = player.Direction.X;
            float front = Math.Max(0f, (damageDirX * player.Direction.X) + (damageDirY * player.Direction.Y));
            float back = Math.Max(0f, -((damageDirX * player.Direction.X) + (damageDirY * player.Direction.Y)));
            float right = Math.Max(0f, (damageDirX * rightX) + (damageDirY * rightY));
            float left = Math.Max(0f, -((damageDirX * rightX) + (damageDirY * rightY)));

            float maxComponent = Math.Max(Math.Max(front, back), Math.Max(left, right));
            if (maxComponent <= 0.001f)
            {
                maxComponent = 1f;
            }

            front = (float)Math.Pow(front / maxComponent, 1.6f);
            back = (float)Math.Pow(back / maxComponent, 1.6f);
            left = (float)Math.Pow(left / maxComponent, 1.6f);
            right = (float)Math.Pow(right / maxComponent, 1.6f);

            int topAlpha = (int)(82f * damageFlash * front);
            int bottomAlpha = (int)(82f * damageFlash * back);
            int leftAlpha = (int)(82f * damageFlash * left);
            int rightAlpha = (int)(82f * damageFlash * right);
            float edgeSize = Math.Max(40f, frameW * 0.065f);

            if (topAlpha > 0)
            {
                r.DrawRectangle(0f, 0f, frameW, edgeSize, Color.FromArgb(topAlpha, 140, 18, 18));
            }

            if (bottomAlpha > 0)
            {
                r.DrawRectangle(0f, frameH - edgeSize, frameW, edgeSize, Color.FromArgb(bottomAlpha, 140, 18, 18));
            }

            if (leftAlpha > 0)
            {
                r.DrawRectangle(0f, edgeSize, edgeSize, frameH - edgeSize * 2f, Color.FromArgb(leftAlpha, 140, 18, 18));
            }

            if (rightAlpha > 0)
            {
                r.DrawRectangle(frameW - edgeSize, edgeSize, edgeSize, frameH - edgeSize * 2f, Color.FromArgb(rightAlpha, 140, 18, 18));
            }
        }

        /// <summary>
        /// GPU 경로에서 무기 이미지를 화면 하단 중앙에 렌더러를 통해 그린다.
        /// 무기별 Gun/ 폴더의 sprite sheet를 우선 사용하고, 없으면 레거시 프레임으로 폴백한다.
        /// </summary>
        private void DrawWeaponOverlay(Renderer r, Weapon weapon)
        {
            if (r == null || weapon == null) return;

            int frameIndex = weapon.GetCurrentWeaponFrameIndex();
            int weaponW = Math.Min((int)(frameW * 0.21f), 210);
            int weaponH = weaponW;
            float startX = (frameW - weaponW) / 2f;
            float startY = frameH - weaponH;

            Image image = textureManager.GetWeaponFireImage(weapon.CurrentType, frameIndex);

            if (image != null)
            {
                r.DrawImage(image, startX, startY, weaponW, weaponH);
            }
        }

        /// <summary>
        /// GPU 경로에서 플레이어 체력 바를 화면 좌측 하단에 렌더러를 통해 그린다.
        /// </summary>
        /// /// <param name="r">사각형을 그릴 렌더러. null이면 아무것도 그리지 않는다.</param>
        /// <param name="player">현재 체력과 최대 체력을 제공하는 플레이어 상태.</param>
        private void DrawHealthBar(Renderer r, Player player)
        {
            if (r == null || player == null)
            {
                return;
            }

            int barW = 164;
            int barH = 10;
            int x = 8;
            int y = frameH - 36;
            r.DrawRectangle(x - 2, y - 2, barW + 4, barH + 4, Color.FromArgb(150, 0, 0, 0));
            float ratio = player.MaxHealth > 0f ? player.Health / player.MaxHealth : 0f;
            if (ratio < 0f) ratio = 0f;
            if (ratio > 1f) ratio = 1f;
            r.DrawRectangle(x, y, barW * ratio, barH, Color.FromArgb(220, 210, 55, 55));
        }

        /// <summary>
        /// GPU 경로에서 플레이어 보호막 바를 화면 좌측 하단에 렌더러를 통해 그린다.
        /// </summary>
        private void DrawShieldBar(Renderer r, Player player)
        {
            if (r == null || player == null)
            {
                return;
            }

            int barW = 164;
            int barH = 8;
            int x = 8;
            int y = frameH - 52;
            r.DrawRectangle(x - 2, y - 2, barW + 4, barH + 4, Color.FromArgb(145, 0, 0, 0));
            float ratio = player.MaxShield > 0f ? player.Shield / player.MaxShield : 0f;
            if (ratio < 0f) ratio = 0f;
            if (ratio > 1f) ratio = 1f;
            r.DrawRectangle(x, y, barW * ratio, barH, Color.FromArgb(220, 82, 185, 235));
        }

        /// <summary>
        /// GPU 경로에서 플레이어 스태미나 바를 화면 좌측 하단에 렌더러를 통해 그린다.
        /// </summary>
        /// <param name="r">사각형을 그릴 렌더러. null이면 아무것도 그리지 않는다.</param>
        /// <param name="player">현재 스태미나와 최대 스태미나를 제공하는 플레이어 상태.</param>
        private void DrawStaminaBar(Renderer r, Player player)
        {
            if (r == null || player == null)
            {
                return;
            }

            int barW = 128;
            int barH = 8;
            int x = 8;
            int y = frameH - barH - 10;
            r.DrawRectangle(x - 2, y - 2, barW + 4, barH + 4, Color.FromArgb(150, 0, 0, 0));
            float ratio = player.MaxStamina > 0f ? player.Stamina / player.MaxStamina : 0f;
            if (ratio < 0f) ratio = 0f;
            if (ratio > 1f) ratio = 1f;
            r.DrawRectangle(x, y, barW * ratio, barH, Color.LimeGreen);
        }

        /// <summary>
        /// GPU 경로에서 화면 정중앙에 조준 십자선을 렌더러를 통해 그린다.
        /// </summary>
        /// <param name="r">사각형을 그릴 렌더러. null이면 아무것도 그리지 않는다.</param>
        private void DrawCrosshair(Renderer r)
        {
            if (r == null)
            {
                return;
            }

            int cx = frameW / 2;
            int cy = frameH / 2;
            r.DrawRectangle(cx - 1, cy - 8, 2, 6, Color.White);
            r.DrawRectangle(cx - 1, cy + 3, 2, 6, Color.White);
            r.DrawRectangle(cx - 8, cy - 1, 6, 2, Color.White);
            r.DrawRectangle(cx + 3, cy - 1, 6, 2, Color.White);
            r.DrawRectangle(cx - 1, cy - 1, 2, 2, Color.FromArgb(220, 255, 240, 160));
        }

        /// <summary>
        /// 명중/처치 피드백을 조준점 주변에 짧게 표시한다.
        /// </summary>
        private void DrawHitMarker(Renderer r, float hitMarkerAlpha, float killMarkerAlpha)
        {
            if (r == null)
            {
                return;
            }

            hitMarkerAlpha = Math.Max(0f, Math.Min(1f, hitMarkerAlpha));
            killMarkerAlpha = Math.Max(0f, Math.Min(1f, killMarkerAlpha));
            float alpha = Math.Max(hitMarkerAlpha, killMarkerAlpha);
            if (alpha <= 0f)
            {
                return;
            }

            int cx = frameW / 2;
            int cy = frameH / 2;
            int baseAlpha = (int)(210f * alpha);
            Color hitColor = Color.FromArgb(baseAlpha, 255, 255, 255);
            DrawDottedHitMarker(r, cx, cy, 10f + (1f - alpha) * 4f, 2f, hitColor);

            if (killMarkerAlpha > 0f)
            {
                int killAlpha = (int)(235f * killMarkerAlpha);
                Color killColor = Color.FromArgb(killAlpha, 255, 92, 64);
                DrawDottedHitMarker(r, cx, cy, 15f + (1f - killMarkerAlpha) * 6f, 3f, killColor);
                r.DrawRectangle(cx - 3, cy - 3, 6, 6, Color.FromArgb((int)(120f * killMarkerAlpha), 255, 190, 95));
            }
        }

        private static void DrawDottedHitMarker(Renderer r, int cx, int cy, float startOffset, float blockSize, Color color)
        {
            for (int i = 0; i < 3; i++)
            {
                float offset = startOffset + i * (blockSize + 1.5f);
                r.DrawRectangle(cx - offset - blockSize, cy - offset - blockSize, blockSize, blockSize, color);
                r.DrawRectangle(cx + offset, cy - offset - blockSize, blockSize, blockSize, color);
                r.DrawRectangle(cx - offset - blockSize, cy + offset, blockSize, blockSize, color);
                r.DrawRectangle(cx + offset, cy + offset, blockSize, blockSize, color);
            }
        }

        /// <summary>발사 불가 상태를 조준점 아래에 짧게 표시한다.</summary>
        private void DrawWeaponStatusFeedback(Renderer r, string statusText, float alpha)
        {
            if (r == null || string.IsNullOrWhiteSpace(statusText))
            {
                return;
            }

            alpha = Math.Max(0f, Math.Min(1f, alpha));
            if (alpha <= 0f)
            {
                return;
            }

            Color color = statusText.IndexOf("AMMO", StringComparison.OrdinalIgnoreCase) >= 0
                ? Color.FromArgb((int)(235f * alpha), 255, 84, 70)
                : Color.FromArgb((int)(220f * alpha), 255, 205, 92);

            r.DrawTextCenteredShadow(statusText, frameW * 0.5f, (frameH * 0.5f) + 34f, color, 10.5f);
        }

        /// <summary>보상 드롭/획득 토스트를 화면 오른쪽 하단 HUD 위에 표시한다.</summary>
        private void DrawPickupToast(Renderer r, string toastText, float alpha)
        {
            if (r == null || string.IsNullOrWhiteSpace(toastText))
            {
                return;
            }

            alpha = Math.Max(0f, Math.Min(1f, alpha));
            if (alpha <= 0f)
            {
                return;
            }

            float panelW = Math.Min(182f, Math.Max(92f, toastText.Length * 7.2f + 24f));
            float panelH = 26f;
            float panelX = frameW - panelW - 18f;
            float panelY = frameH - 174f - ((1f - alpha) * 9f);
            r.DrawRectangle(panelX, panelY, panelW, panelH, Color.FromArgb((int)(120f * alpha), 10, 10, 10));
            r.DrawRectangle(panelX, panelY + panelH - 3f, panelW, 3f, GetPickupToastAccentColor(toastText, alpha));
            r.DrawTextCenteredShadow(toastText, panelX + panelW * 0.5f, panelY + 13f, GetPickupToastTextColor(toastText, alpha), 9.5f);
        }

        private static Color GetPickupToastAccentColor(string text, float alpha)
        {
            if (text.IndexOf("COIN", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Color.FromArgb((int)(220f * alpha), 255, 205, 82);
            }

            if (text.IndexOf("AMMO", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Color.FromArgb((int)(210f * alpha), 108, 195, 255);
            }

            return Color.FromArgb((int)(210f * alpha), 255, 105, 95);
        }

        private static Color GetPickupToastTextColor(string text, float alpha)
        {
            Color accent = GetPickupToastAccentColor(text, alpha);
            return Color.FromArgb((int)(235f * alpha), accent.R, accent.G, accent.B);
        }

        /// <summary>
        /// GPU 경로에서 사망 연출 전체(붉은 색조, 비네트, "YOU DIED" 패널 및 텍스트)를
        /// 렌더러를 통해 그린다.
        /// </summary>
        /// <param name="r">사각형과 텍스트를 그릴 렌더러. null이면 아무것도 그리지 않는다.</param>
        /// <param name="deathProgress">사망 연출 진행 비율(0=시작, 1=완료).</param>
        private void DrawDeathOverlay(Renderer r, float deathProgress)
        {
            if (r == null)
            {
                return;
            }

            float eased = EaseOutCubic(deathProgress);
            int redAlpha = (int)(55f + (145f * eased));
            int vignetteAlpha = (int)(20f + (95f * eased));
            r.DrawRectangle(0f, 0f, frameW, frameH, Color.FromArgb(redAlpha, 125, 10, 10));
            r.DrawRectangle(0f, 0f, frameW, frameH * 0.22f, Color.FromArgb(vignetteAlpha, 40, 0, 0));
            r.DrawRectangle(0f, frameH * 0.78f, frameW, frameH * 0.22f, Color.FromArgb(vignetteAlpha, 55, 0, 0));

            float uiFade = deathProgress <= 0.2f ? 0f : (deathProgress - 0.2f) / 0.8f;
            uiFade = EaseOutCubic(uiFade);
            if (uiFade > 0f)
            {
                float panelW = Math.Min(frameW * 0.48f, 470f);
                float panelH = 132f;
                float panelX = (frameW - panelW) * 0.5f;
                float panelY = frameH * 0.39f;
                r.DrawRectangle(panelX, panelY, panelW, panelH, Color.FromArgb((int)(180f * uiFade), 8, 0, 0));
                r.DrawRectangle(panelX + 10f, panelY + 10f, panelW - 20f, panelH - 20f, Color.FromArgb((int)(105f * uiFade), 85, 8, 8));
            }

            DrawDeathPresentation(r, deathProgress);
        }
    }
}
