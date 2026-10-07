using System;
using System.Collections.Generic;
using System.Drawing;
using My2DEngine.Engine.Rendering;
using My2DEngine.Game.Config;
using My2DEngine.Game.Core;
using My2DEngine.Game.Systems;

namespace My2DEngine.Game.Rendering
{
    /// <summary>
    /// GPU 월드 렌더링 위에 얹는 월드 오버레이를 담당하는 partial 클래스.
    /// 적 체력 바, 텔레그래프, 픽업 테두리 같은 부가 요소를 GPU 오버레이 큐로 전달한다.
    /// </summary>
    public partial class RaycastRenderer
    {
        /// <summary>
        /// 살아있거나 렌더링 가능한 적들을 거리 순으로 정렬한 뒤 각각 투영하여
        /// 체력 바와 텔레그래프 오버레이를 GPU 큐에 추가한다.
        /// </summary>
        /// <param name="player">카메라 위치·방향·투영 평면을 제공하는 플레이어 상태.</param>
        private void RenderEnemies(Player player)
        {
            Enemy[] enemies = enemyManager.Enemies;
            if (enemies == null || enemies.Length == 0)
            {
                return;
            }

            if (spriteDepthBuffer == null || spriteDepthBuffer.Length < frameW)
            {
                spriteDepthBuffer = new float[frameW];
            }
            for (int i = 0; i < frameW; i++)
            {
                spriteDepthBuffer[i] = float.MaxValue;
            }

            int count = enemies.Length;
            EnsureEnemySortBuffers(count);
            double[] sortKeys = enemySortKeys;
            int[] order = enemyOrder;
            float playerX = player.Position.X;
            float playerY = player.Position.Y;
            double planeX = player.Plane.X;
            double planeY = player.Plane.Y;
            double dirX = player.Direction.X;
            double dirY = player.Direction.Y;
            double invDet = 1.0 / (planeX * dirY - dirX * planeY);
            int renderableCount = 0;

            for (int i = 0; i < count; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy == null || !enemy.IsRenderable)
                {
                    continue;
                }

                float dx = enemy.X - playerX;
                float dy = enemy.Y - playerY;
                sortKeys[renderableCount] = -(dx * dx + dy * dy);
                order[renderableCount] = i;
                renderableCount++;
            }

            if (renderableCount == 0)
            {
                return;
            }

            if (renderableCount <= 32)
            {
                for (int i = 1; i < renderableCount; i++)
                {
                    double keyDistance = sortKeys[i];
                    int keyIndex = order[i];
                    int j = i - 1;

                    while (j >= 0 && sortKeys[j] < keyDistance)
                    {
                        sortKeys[j + 1] = sortKeys[j];
                        order[j + 1] = order[j];
                        j--;
                    }

                    sortKeys[j + 1] = keyDistance;
                    order[j + 1] = keyIndex;
                }
            }
            else
            {
                Array.Sort(sortKeys, order, 0, renderableCount);
            }

            for (int i = 0; i < renderableCount; i++)
            {
                Enemy enemy = enemies[order[i]];

                double spriteX = enemy.X - playerX;
                double spriteY = enemy.Y - playerY;
                double transformX = invDet * (dirY * spriteX - dirX * spriteY);
                double transformY = invDet * (-planeY * spriteX + planeX * spriteY);

                if (transformY <= 0.05)
                {
                    continue;
                }

                int spriteScreenX = (int)((frameW * 0.5) * (1.0 + transformX / transformY));
                float spriteHeightF = Math.Max(6f, (float)Math.Round(Math.Abs((frameH / (float)transformY) * enemy.Scale)));
                int spriteHeight = (int)spriteHeightF;
                if (spriteHeight <= 0)
                {
                    continue;
                }

                float spriteCenterY = (float)Math.Round(frameH * 0.5f);
                int drawStartY = (int)Math.Floor(spriteCenterY - spriteHeightF * 0.5f);
                int drawEndY = (int)Math.Floor(spriteCenterY + spriteHeightF * 0.5f);
                int unclampedDrawStartY = drawStartY;
                if (drawStartY < 0) drawStartY = 0;
                if (drawEndY >= frameH) drawEndY = frameH - 1;

                int spriteWidth = spriteHeight;
                int drawStartX = -spriteWidth / 2 + spriteScreenX;
                int drawEndX = spriteWidth / 2 + spriteScreenX;
                if (drawStartX < 0) drawStartX = 0;
                if (drawEndX >= frameW) drawEndX = frameW - 1;

                Color[] sprite = textureManager.GetAnimatedEnemySprite(enemy) ?? enemy.Sprite;
                if (sprite == null)
                {
                    continue;
                }

                bool enemyVisible = IsSpriteVisibleAgainstDepth(drawStartX, drawEndX, transformY);
                if (!enemyVisible)
                {
                    continue;
                }

                float depthF = (float)transformY;
                int fillStart = Math.Max(0, -spriteWidth / 2 + spriteScreenX);
                int fillEnd   = Math.Min(frameW - 1, spriteWidth / 2 + spriteScreenX);
                for (int sx = fillStart; sx <= fillEnd; sx++)
                {
                    if (depthF < spriteDepthBuffer[sx])
                        spriteDepthBuffer[sx] = depthF;
                }

                if ((enemy.IsBoss || enemy.IsMiniBoss) && enemy.TelegraphTimer > 0f)
                {
                    DrawEnemyTelegraphAura(drawStartX, drawEndX, drawStartY, drawEndY,
                        GetEnemyTelegraphColor(enemy),
                        GetEnemyTelegraphIntensity(enemy),
                        (float)transformY);
                }

                if (enemy.Alive && !enemy.IsSpawning)
                {
                    int enemyBarY = unclampedDrawStartY - 10;
                    if (enemyBarY < 6) enemyBarY = 6;
                    int enemyBarW = spriteWidth;
                    if (enemyBarW < 24) enemyBarW = 24;
                    if (enemyBarW > 104) enemyBarW = 104;
                    float enemyHealthRatio = enemy.MaxHealth > 0f ? enemy.Health / enemy.MaxHealth : 0f;
                    DrawWorldHealthBar(spriteScreenX, enemyBarY, enemyBarW, 5, enemyHealthRatio,
                        enemy.IsObjectiveTarget && enemy.ObjectiveTargetRevealed
                            ? Color.FromArgb(245, 255, 220, 65)
                            : enemy.IsBoss
                            ? Color.FromArgb(240, 255, 175, 60)
                            : enemy.IsMiniBoss
                                ? Color.FromArgb(235, 90, 180, 255)
                                : Color.FromArgb(235, 210, 55, 55),
                        (float)transformY);
                }
            }
        }

        /// <summary>
        /// 적의 공격 예고 아우라(글로우 효과)를 해당 스프라이트 영역 주변에 그린다.
        /// 링 형태의 오버레이 사각형을 큐에 추가해 GPU 경로에서 표시한다.
        /// </summary>
        /// <param name="drawStartX">스프라이트 렌더링 영역의 왼쪽 X 좌표.</param>
        /// <param name="drawEndX">스프라이트 렌더링 영역의 오른쪽 X 좌표.</param>
        /// <param name="drawStartY">스프라이트 렌더링 영역의 위쪽 Y 좌표.</param>
        /// <param name="drawEndY">스프라이트 렌더링 영역의 아래쪽 Y 좌표.</param>
        /// <param name="color">아우라 색상.</param>
        /// <param name="intensity">아우라 강도(0~1 권장).</param>
        /// <param name="depth">월드 공간 깊이. GPU 경로에서 차폐 판정에 사용된다.</param>
        private void DrawEnemyTelegraphAura(int drawStartX, int drawEndX, int drawStartY, int drawEndY, Color color, float intensity, float depth = 0f)
        {
            int padX = 10;
            int padY = 10;
            int left = Math.Max(0, drawStartX - padX);
            int right = Math.Min(frameW - 1, drawEndX + padX);
            int top = Math.Max(0, drawStartY - padY);
            int bottom = Math.Min(frameH - 1, drawEndY + padY);

            QueueTelegraphRectRings(left, top, right, bottom, color, intensity, depth);
        }

        private static Color GetEnemyTelegraphColor(Enemy enemy)
        {
            if (enemy != null && enemy.IsBoss)
            {
                float ratio = enemy.MaxHealth > 0f ? enemy.Health / enemy.MaxHealth : 1f;
                if (ratio <= EnemyConfig.BossPhaseThreeHealthRatio)
                {
                    return Color.FromArgb(220, 255, 55, 45);
                }

                if (ratio <= EnemyConfig.BossPhaseTwoHealthRatio)
                {
                    return Color.FromArgb(205, 255, 155, 55);
                }
            }

            return IsRiftStyleTelegraph(enemy)
                ? Color.FromArgb(180, 110, 150, 255)
                : Color.FromArgb(185, 255, 110, 70);
        }

        private static float GetEnemyTelegraphIntensity(Enemy enemy)
        {
            if (enemy == null)
            {
                return 0.22f;
            }

            float intensity = 0.22f + Math.Min(0.28f, enemy.TelegraphTimer * 0.35f);
            if (enemy.IsBoss)
            {
                float ratio = enemy.MaxHealth > 0f ? enemy.Health / enemy.MaxHealth : 1f;
                if (ratio <= EnemyConfig.BossPhaseThreeHealthRatio)
                {
                    intensity += EnemyConfig.BossPhaseThreeTelegraphIntensityBonus;
                }
                else if (ratio <= EnemyConfig.BossPhaseTwoHealthRatio)
                {
                    intensity += EnemyConfig.BossPhaseTwoTelegraphIntensityBonus;
                }
            }

            return Math.Min(0.72f, intensity);
        }

        private static bool IsRiftStyleTelegraph(Enemy enemy)
        {
            if (enemy == null)
            {
                return false;
            }

            switch (enemy.BehaviorPattern)
            {
                case EnemyBehaviorPattern.BossRunner:
                case EnemyBehaviorPattern.BossRiftBlitz:
                case EnemyBehaviorPattern.BossAgathoDemon:
                case EnemyBehaviorPattern.BossArachnoFang:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// GPU 경로에서 텔레그래프 아우라를 나타내는 3겹의 테두리 링 사각형을
        /// worldOverlayRects 큐에 추가한다. 바깥 링일수록 더 투명해진다.
        /// </summary>
        /// <param name="left">내부 링의 왼쪽 X 좌표.</param>
        /// <param name="top">내부 링의 위쪽 Y 좌표.</param>
        /// <param name="right">내부 링의 오른쪽 X 좌표.</param>
        /// <param name="bottom">내부 링의 아래쪽 Y 좌표.</param>
        /// <param name="color">링 색상(알파 포함).</param>
        /// <param name="intensity">아우라 강도(0~1). 알파 계산에 사용된다.</param>
        /// <param name="depth">월드 공간 깊이. 차폐 판정에 사용된다.</param>
        private void QueueTelegraphRectRings(int left, int top, int right, int bottom, Color color, float intensity, float depth = 0f)
        {
            if (left > right || top > bottom)
            {
                return;
            }

            int alpha0 = (int)(Math.Min(1f, intensity * 1.05f) * 120f);
            int alpha1 = (int)(Math.Min(1f, intensity * 0.75f) * 90f);
            int alpha2 = (int)(Math.Min(1f, intensity * 0.5f) * 68f);
            QueueBorderRing(left, top, right, bottom, 2, Color.FromArgb(alpha0, color), depth);
            QueueBorderRing(left - 4, top - 4, right + 4, bottom + 4, 2, Color.FromArgb(alpha1, color), depth);
            QueueBorderRing(left - 8, top - 8, right + 8, bottom + 8, 2, Color.FromArgb(alpha2, color), depth);
        }

        /// <summary>
        /// 활성화된 보상 픽업을 화면에 투영하고, 희귀도 테두리 효과를 그린다.
        /// 픽업은 바닥 근처에 위치하며 PulseTimer에 따라 위아래로 부유한다.
        /// </summary>
        /// <param name="player">카메라 위치·방향·투영 평면을 제공하는 플레이어 상태.</param>
        /// <param name="rewardPickups">렌더링할 보상 픽업 목록.</param>
        private void RenderRewardPickups(Player player, IList<RewardPickup> rewardPickups)
        {
            if (rewardPickups == null || rewardPickups.Count == 0)
            {
                return;
            }

            float playerX = player.Position.X;
            float playerY = player.Position.Y;
            double planeX = player.Plane.X;
            double planeY = player.Plane.Y;
            double dirX = player.Direction.X;
            double dirY = player.Direction.Y;
            double invDet = 1.0 / (planeX * dirY - dirX * planeY);

            for (int i = 0; i < rewardPickups.Count; i++)
            {
                RewardPickup pickup = rewardPickups[i];
                if (pickup == null || !pickup.Active)
                {
                    continue;
                }

                double spriteX = pickup.X - playerX;
                double spriteY = pickup.Y - playerY;
                double transformX = invDet * (dirY * spriteX - dirX * spriteY);
                double transformY = invDet * (-planeY * spriteX + planeX * spriteY);
                if (transformY <= 0.08)
                {
                    continue;
                }

                int spriteScreenX = (int)((frameW * 0.5) * (1.0 + transformX / transformY));
                float bob = (float)Math.Sin(pickup.PulseTimer * 3.6f) * 0.08f;
                int spriteHeight = Math.Abs((int)((frameH / transformY) * 0.42f));
                if (spriteHeight <= 8)
                {
                    continue;
                }

                int spriteWidth = Math.Max(12, (int)(spriteHeight * 0.82f));
                int groundY = frameH / 2 + (int)((frameH * 0.34f) / transformY) - (int)(bob * frameH * 0.2f);
                int drawStartY = groundY - spriteHeight;
                int drawEndY = groundY;
                int drawStartX = spriteScreenX - spriteWidth / 2;
                int drawEndX = spriteScreenX + spriteWidth / 2;

                if (drawStartX < 0) drawStartX = 0;
                if (drawEndX >= frameW) drawEndX = frameW - 1;
                if (drawStartY < 0) drawStartY = 0;
                if (drawEndY >= frameH) drawEndY = frameH - 1;

                float fog = Fog((float)transformY);
                Color rarityBorderColor = GetPickupRarityBorderColor(pickup);

                bool pickupVisible = IsSpriteVisibleAgainstDepth(drawStartX, drawEndX, transformY);
                if (pickupVisible && rarityBorderColor.A > 0)
                {
                    DrawPickupBorder(drawStartX, drawEndX, drawStartY, drawEndY, rarityBorderColor, fog);
                }
            }
        }

        private void DrawRewardPickupLabels(Renderer r, Player player, IList<RewardPickup> rewardPickups)
        {
            if (r == null || player == null || rewardPickups == null || rewardPickups.Count == 0)
            {
                return;
            }

            float playerX = player.Position.X;
            float playerY = player.Position.Y;
            double planeX = player.Plane.X;
            double planeY = player.Plane.Y;
            double dirX = player.Direction.X;
            double dirY = player.Direction.Y;
            double invDet = 1.0 / (planeX * dirY - dirX * planeY);

            for (int i = 0; i < rewardPickups.Count; i++)
            {
                RewardPickup pickup = rewardPickups[i];
                if (pickup == null || !pickup.Active || !pickup.IsRestChoice)
                {
                    continue;
                }

                double spriteX = pickup.X - playerX;
                double spriteY = pickup.Y - playerY;
                double transformX = invDet * (dirY * spriteX - dirX * spriteY);
                double transformY = invDet * (-planeY * spriteX + planeX * spriteY);
                if (transformY <= 0.08)
                {
                    continue;
                }

                int spriteScreenX = (int)((frameW * 0.5) * (1.0 + transformX / transformY));
                if (spriteScreenX < -48 || spriteScreenX > frameW + 48)
                {
                    continue;
                }

                float bob = (float)Math.Sin(pickup.PulseTimer * 3.6f) * 0.08f;
                int spriteHeight = Math.Abs((int)((frameH / transformY) * 0.42f));
                if (spriteHeight <= 8)
                {
                    continue;
                }

                int groundY = frameH / 2 + (int)((frameH * 0.34f) / transformY) - (int)(bob * frameH * 0.2f);
                float labelY = Math.Max(18f, groundY - spriteHeight - 10f);
                r.DrawTextCenteredShadow(GetRestPickupLabel(pickup), spriteScreenX, labelY, GetRestPickupLabelColor(pickup), 6f);
            }
        }

        /// <summary>
        /// 픽업의 희귀도에 따라 테두리 색상을 반환한다.
        /// 상점 카드는 카드 등급 색상, 일반 픽업은 희귀도 색상을 반환한다.
        /// </summary>
        /// <param name="pickup">희귀도 정보를 가진 보상 픽업.</param>
        /// <returns>희귀도에 해당하는 테두리 색상. 희귀도가 없으면 투명.</returns>
        private Color GetPickupRarityBorderColor(RewardPickup pickup)
        {
            if (pickup == null)
            {
                return Color.Transparent;
            }

            if (pickup.Kind == RewardPickupKind.Card && pickup.CardOffer != null)
            {
                Color gradeColor = GetCardOfferColor(pickup.CardOffer);
                return Color.FromArgb(235, gradeColor);
            }

            if (pickup.IsRestChoice)
            {
                return Color.FromArgb(220, 255, 210, 110);
            }

            if (pickup.Kind == RewardPickupKind.AmmoPack)
            {
                return Color.Transparent;
            }

            switch (pickup.Rarity)
            {
                case RewardPickupRarity.None:
                    return pickup.Kind == RewardPickupKind.Coin
                        ? Color.FromArgb(220, 255, 210, 110)
                        : Color.Transparent;
                case RewardPickupRarity.Common:
                    return Color.FromArgb(235, 225, 225, 225);
                case RewardPickupRarity.Rare:
                    return Color.FromArgb(235, 90, 170, 255);
                case RewardPickupRarity.Epic:
                    return Color.FromArgb(235, 230, 120, 255);
                default:
                    return Color.Transparent;
            }
        }

        /// <summary>
        /// 픽업 스프라이트 주변에 희귀도 테두리 사각형을 그린다.
        /// GPU 오버레이 큐에 테두리 링을 추가한다.
        /// </summary>
        /// <param name="drawStartX">픽업 스프라이트 렌더 영역의 왼쪽 X 좌표.</param>
        /// <param name="drawEndX">픽업 스프라이트 렌더 영역의 오른쪽 X 좌표.</param>
        /// <param name="drawStartY">픽업 스프라이트 렌더 영역의 위쪽 Y 좌표.</param>
        /// <param name="drawEndY">픽업 스프라이트 렌더 영역의 아래쪽 Y 좌표.</param>
        /// <param name="borderColor">테두리 색상.</param>
        /// <param name="fog">안개 계수. 테두리 색상에 적용된다.</param>
        private void DrawPickupBorder(int drawStartX, int drawEndX, int drawStartY, int drawEndY, Color borderColor, float fog)
        {
            int left = Math.Max(0, drawStartX - 2);
            int right = Math.Min(frameW - 1, drawEndX + 2);
            int top = Math.Max(0, drawStartY - 2);
            int bottom = Math.Min(frameH - 1, drawEndY + 2);

            if (left > right || top > bottom)
            {
                return;
            }

            QueueBorderRing(left, top, right, bottom, 2, Color.FromArgb(borderColor.A, Color.FromArgb(DarkenFast(borderColor.ToArgb(), fog))));
        }

        private string GetRestPickupLabel(RewardPickup pickup)
        {
            if (pickup == null)
            {
                return string.Empty;
            }

            string itemLabel;
            switch (pickup.Kind)
            {
                case RewardPickupKind.Card:
                    itemLabel = GetRestShopCardLabel(pickup.CardOffer);
                    break;
                case RewardPickupKind.AmmoPack:
                    itemLabel = "탄약";
                    break;
                default:
                    itemLabel = "아이템";
                    break;
            }

            int cost = Math.Max(0, pickup.CoinCost);
            return cost > 0 ? itemLabel + " " + cost + "C" : itemLabel;
        }

        private Color GetRestPickupLabelColor(RewardPickup pickup)
        {
            if (pickup == null)
            {
                return Color.White;
            }

            switch (pickup.Kind)
            {
                case RewardPickupKind.Card:
                    return pickup.CardOffer != null
                        ? GetCardOfferColor(pickup.CardOffer)
                        : Color.FromArgb(255, 255, 225, 120);
                case RewardPickupKind.AmmoPack:
                    return Color.FromArgb(255, 255, 220, 120);
                default:
                    return Color.FromArgb(255, 220, 220, 220);
            }
        }

        private static Color GetCardOfferColor(RewardCardOffer offer)
        {
            if (offer == null)
            {
                return Color.FromArgb(255, 255, 225, 120);
            }

            return offer.IsWeaponCard
                ? CardGradeHelper.GetGradeColor(offer.WeaponGrade)
                : CardGradeHelper.GetGradeColor(offer.Grade);
        }

        private static string GetRestShopCardLabel(RewardCardOffer offer)
        {
            if (offer == null)
            {
                return "카드";
            }

            CardGrade grade = offer.IsWeaponCard ? offer.WeaponGrade : offer.Grade;
            string gradeName = CardGradeHelper.GetGradeName(grade);
            if (offer.IsWeaponCard)
            {
                return gradeName + " " + WeaponPresentation.GetDisplayName(offer.WeaponType);
            }

            return gradeName + " " + GetShortStatName(offer.StatType);
        }

        private static string GetShortStatName(StatType stat)
        {
            return StatCardCatalog.Get(stat).ShortName;
        }

        /// <summary>
        /// 적 스프라이트 위에 체력 바를 그리기 위한 월드 오버레이 사각형을 큐에 추가한다.
        /// </summary>
        /// <param name="centerX">체력 바의 수평 중앙 X 좌표(픽셀).</param>
        /// <param name="y">체력 바의 위쪽 Y 좌표(픽셀).</param>
        /// <param name="width">체력 바의 전체 너비(픽셀).</param>
        /// <param name="height">체력 바의 높이(픽셀).</param>
        /// <param name="ratio">현재 체력 비율(0~1). 1이면 가득 찬 상태.</param>
        /// <param name="fillColor">체력 채움 부분의 색상.</param>
        /// <param name="depth">월드 공간 깊이. 차폐 판정에 사용된다. 0 이하이면 항상 표시한다.</param>
        private void DrawWorldHealthBar(int centerX, int y, int width, int height, float ratio, Color fillColor, float depth = 0f)
        {
            if (height <= 0 || width <= 0)
            {
                return;
            }

            if (ratio < 0f) ratio = 0f;
            if (ratio > 1f) ratio = 1f;

            int barW = width;
            int barX = centerX - (barW / 2);
            QueueWorldOverlayRect(barX - 2, y - 2, barW + 4, height + 4, Color.FromArgb(155, 0, 0, 0), depth);
            QueueWorldOverlayRect(barX, y, barW, height, Color.FromArgb(170, 35, 35, 35), depth);
            int fillWidth = (int)(barW * ratio);
            if (fillWidth > 0)
            {
                QueueWorldOverlayRect(barX, y, fillWidth, height, fillColor, depth);
            }
        }

        /// <summary>
        /// GPU 경로에서 테두리 링(속이 빈 사각형)을 worldOverlayRects 큐에 추가한다.
        /// 상·하·좌·우 4개의 얇은 사각형으로 테두리를 구성한다.
        /// </summary>
        /// <param name="left">테두리의 왼쪽 X 좌표.</param>
        /// <param name="top">테두리의 위쪽 Y 좌표.</param>
        /// <param name="right">테두리의 오른쪽 X 좌표.</param>
        /// <param name="bottom">테두리의 아래쪽 Y 좌표.</param>
        /// <param name="thickness">테두리 두께(픽셀).</param>
        /// <param name="color">테두리 색상(알파 포함).</param>
        /// <param name="depth">월드 공간 깊이. 차폐 판정에 사용된다.</param>
        private void QueueBorderRing(int left, int top, int right, int bottom, int thickness, Color color, float depth = 0f)
        {
            if (left > right || top > bottom || thickness <= 0 || color.A <= 0)
            {
                return;
            }

            int width = right - left + 1;
            int height = bottom - top + 1;
            QueueWorldOverlayRect(left, top, width, thickness, color, depth);
            QueueWorldOverlayRect(left, bottom - thickness + 1, width, thickness, color, depth);
            QueueWorldOverlayRect(left, top + thickness, thickness, Math.Max(0, height - thickness * 2), color, depth);
            QueueWorldOverlayRect(right - thickness + 1, top + thickness, thickness, Math.Max(0, height - thickness * 2), color, depth);
        }

        /// <summary>
        /// 스프라이트의 화면 X 범위 내에서 zBuffer를 검사하여
        /// 적어도 하나의 열(column)에서 스프라이트가 벽 앞에 있는지 판정한다.
        /// 가시 열이 하나라도 있으면 true를 반환한다.
        /// </summary>
        /// <param name="drawStartX">스프라이트 렌더 영역의 왼쪽 X 좌표.</param>
        /// <param name="drawEndX">스프라이트 렌더 영역의 오른쪽 X 좌표.</param>
        /// <param name="transformY">스프라이트의 투영 깊이(transformY).</param>
        /// <returns>스프라이트가 하나 이상의 열에서 벽 앞에 있으면 true, 완전히 가려지면 false.</returns>
        private bool IsSpriteVisibleAgainstDepth(int drawStartX, int drawEndX, double transformY)
        {
            if (zBuffer == null || drawEndX < drawStartX)
            {
                return false;
            }

            int start = Math.Max(0, drawStartX);
            int end = Math.Min(frameW - 1, drawEndX);
            for (int x = start; x <= end; x++)
            {
                if (transformY < zBuffer[x])
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 적 정렬에 사용하는 거리 키 배열과 인덱스 배열이 충분한 크기인지 확인하고,
        /// 필요하면 새로 할당한다.
        /// </summary>
        /// <param name="count">정렬할 적의 최대 수. 현재 크기보다 크면 배열을 재할당한다.</param>
        private void EnsureEnemySortBuffers(int count)
        {
            if (enemySortKeys == null || enemySortKeys.Length < count)
            {
                enemySortKeys = new double[count];
            }
            if (enemyOrder == null || enemyOrder.Length < count)
            {
                enemyOrder = new int[count];
            }
        }

    }
}
