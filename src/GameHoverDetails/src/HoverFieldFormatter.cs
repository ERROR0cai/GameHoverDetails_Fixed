using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace GameHoverDetails
{
    internal static class HoverFieldFormatter
    {
        public static string Format(string key, Game game, IPlayniteAPI api)
        {
            if (game == null || string.IsNullOrEmpty(key))
            {
                return HoverLoc.Empty;
            }

            try
            {
                switch (key)
                {
                    case "Name":
                        return game.Name ?? HoverLoc.Empty;
                    case "Description":
                        return string.IsNullOrWhiteSpace(game.Description) ? HoverLoc.Empty : game.Description;
                    case "Platform":
                        return JoinNames(game.Platforms);
                    case "Genre":
                        return JoinNames(game.Genres);
                    case "Developer":
                        return JoinNames(game.Developers);
                    case "Publisher":
                        return JoinNames(game.Publishers);
                    case "Category":
                        return JoinNames(game.Categories);
                    case "Tags":
                        return JoinNames(game.Tags);
                    case "Features":
                        return JoinNames(game.Features);
                    case "Series":
                        return JoinNames(game.Series);
                    case "Region":
                        return JoinNames(game.Regions);
                    case "AgeRating":
                        return JoinNames(game.AgeRatings);
                    case "Version":
                        return string.IsNullOrWhiteSpace(game.Version) ? HoverLoc.Empty : game.Version;
                    case "Notes":
                        return string.IsNullOrWhiteSpace(game.Notes) ? HoverLoc.Empty : game.Notes;
                    case "InstallationFolder":
                        return string.IsNullOrWhiteSpace(game.InstallDirectory) ? HoverLoc.Empty : game.InstallDirectory;
                    case "InstallSize":
                        return FormatInstallSize(game.InstallSize);
                    case "ReleaseDate":
                        return FormatReleaseDateLong(game.ReleaseDate);
                    case "DateAdded":
                        return game.Added == null
                            ? HoverLoc.Empty
                            : FormatDateNoWeekday(game.Added.Value.ToLocalTime());
                    case "TimePlayed":
                        return FormatPlaytime(game.Playtime);
                    case "RecentActivity":
                        return game.RecentActivity == null
                            ? HoverLoc.Empty
                            : FormatDateNoWeekday(game.RecentActivity.Value.ToLocalTime());
                    case "LastPlayed":
                        return FormatLastPlayedDate(game.LastActivity);
                    case "CompletionStatus":
                        return game.CompletionStatus?.Name ?? HoverLoc.Empty;
                    case "UserScore":
                        return FormatNullableScore(game.UserScore);
                    case "CriticScore":
                        return FormatNullableScore(game.CriticScore);
                    case "CommunityScore":
                        return FormatNullableScore(game.CommunityScore);
                    case "Source":
                        return game.Source?.Name ?? HoverLoc.Empty;
                    case "Library":
                        return FormatLibrary(game, api);
                    case "Links":
                        return FormatLinks(game);
                    case "Icon":
                    case "CoverImage":
                    case "BackgroundImage":
                        return string.Empty;
                    default:
                        return HoverLoc.Empty;
                }
            }
            catch
            {
                return HoverLoc.Empty;
            }
        }

        private static string FormatLibrary(Game game, IPlayniteAPI api)
        {
            try
            {
                var plugins = api?.Addons?.Plugins;
                if (plugins != null)
                {
                    foreach (var plugin in plugins)
                    {
                        if (plugin == null || plugin.Id != game.PluginId)
                        {
                            continue;
                        }

                        if (plugin is LibraryPlugin library)
                        {
                            var name = library.Name;
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                return name;
                            }
                        }

                        break;
                    }
                }
            }
            catch
            {
                // ignore
            }

            return HoverLoc.Get("LOCGameHoverDetails_Value_UnknownLibrary", "Unknown library");
        }

        /// <summary>Month, day, and year in current culture without weekday (e.g. April 15, 2026).</summary>
        private static string FormatDateNoWeekday(DateTime localDateTime)
        {
            return localDateTime.Date.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture);
        }

        private static string FormatReleaseDateLong(ReleaseDate? releaseDate)
        {
            if (releaseDate == null)
            {
                return HoverLoc.Empty;
            }

            var rd = releaseDate.Value;
            if (rd.Equals(ReleaseDate.Empty))
            {
                return HoverLoc.Empty;
            }

            var culture = CultureInfo.CurrentCulture;
            try
            {
                if (rd.Month != null && rd.Day != null)
                {
                    var dt = new DateTime(rd.Year, rd.Month.Value, rd.Day.Value);
                    return FormatDateNoWeekday(dt);
                }

                if (rd.Month != null)
                {
                    var dt = new DateTime(rd.Year, rd.Month.Value, 1);
                    return dt.ToString("MMMM yyyy", culture);
                }

                return rd.Year.ToString(culture);
            }
            catch
            {
                return rd.Year.ToString(culture);
            }
        }

        private static string FormatLastPlayedDate(DateTime? lastActivityUtc)
        {
            if (lastActivityUtc == null)
            {
                return HoverLoc.Empty;
            }

            var local = lastActivityUtc.Value.ToLocalTime();
            var now = DateTime.Now;
            if (local > now)
            {
                return HoverLoc.Empty;
            }

            var elapsed = now - local;
            if (elapsed.TotalDays >= 30)
            {
                return FormatDateNoWeekday(local);
            }

            return HoverLoc.Format(
                "LOCGameHoverDetails_Value_RelativeAgo",
                "{0} ago",
                FormatRelativeElapsed(elapsed));
        }

        /// <summary>Relative unit phrase for Last Played within the past 30 days (e.g. "2m", "3h").</summary>
        private static string FormatRelativeElapsed(TimeSpan elapsed)
        {
            var totalSeconds = (int)elapsed.TotalSeconds;
            if (totalSeconds < 60)
            {
                return Math.Max(1, totalSeconds) + HoverLoc.Get("LOCGameHoverDetails_Value_UnitSec", "s");
            }

            var totalMinutes = (int)elapsed.TotalMinutes;
            if (totalMinutes < 60)
            {
                return totalMinutes + HoverLoc.Get("LOCGameHoverDetails_Value_UnitMin", "m");
            }

            var totalHours = (int)elapsed.TotalHours;
            if (totalHours < 24)
            {
                return totalHours + HoverLoc.Get("LOCGameHoverDetails_Value_UnitHour", "h");
            }

            var totalDays = (int)elapsed.TotalDays;
            if (totalDays < 7)
            {
                return totalDays + HoverLoc.Get("LOCGameHoverDetails_Value_UnitDay", "d");
            }

            var totalWeeks = totalDays / 7;
            return totalWeeks + HoverLoc.Get("LOCGameHoverDetails_Value_UnitWeek", "w");
        }

        private static string FormatLinks(Game game)
        {
            if (game.Links == null || game.Links.Count == 0)
            {
                return HoverLoc.Empty;
            }

            var sb = new StringBuilder();
            foreach (var link in game.Links)
            {
                if (link == null)
                {
                    continue;
                }

                var name = string.IsNullOrWhiteSpace(link.Name)
                    ? HoverLoc.Get("LOCGameHoverDetails_Value_Link", "Link")
                    : link.Name;
                var url = link.Url ?? "";
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }

                sb.Append(name).Append(": ").Append(url);
            }

            return sb.Length == 0 ? HoverLoc.Empty : sb.ToString();
        }

        private static string FormatNullableScore(int? score)
        {
            return score == null || score.Value <= 0 ? HoverLoc.Empty : score.Value.ToString(CultureInfo.CurrentCulture);
        }

        private static string FormatInstallSize(ulong? bytes)
        {
            if (bytes == null || bytes.Value == 0UL)
            {
                return HoverLoc.Empty;
            }

            var b = (double)bytes.Value;
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            var order = 0;
            while (b >= 1024 && order < units.Length - 1)
            {
                order++;
                b /= 1024;
            }

            return string.Format(CultureInfo.CurrentCulture, "{0:0.##} {1}", b, units[order]);
        }

        private static string FormatPlaytime(ulong playtimeSeconds)
        {
            if (playtimeSeconds == 0UL)
            {
                return HoverLoc.Empty;
            }

            var ts = TimeSpan.FromSeconds(playtimeSeconds);
            if (ts.TotalDays >= 1)
            {
                return string.Format(CultureInfo.CurrentCulture, "{0}d {1}h", (int)ts.TotalDays, ts.Hours);
            }

            if (ts.TotalHours >= 1)
            {
                return string.Format(CultureInfo.CurrentCulture, "{0}h {1}m", (int)ts.TotalHours, ts.Minutes);
            }

            return string.Format(CultureInfo.CurrentCulture, "{0}m", (int)ts.TotalMinutes);
        }

        private static string JoinNames(IEnumerable<object> items)
        {
            if (items == null)
            {
                return HoverLoc.Empty;
            }

            var names = new List<string>();
            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }

                var nameProp = item.GetType().GetProperty("Name");
                var n = nameProp?.GetValue(item, null) as string;
                if (!string.IsNullOrWhiteSpace(n))
                {
                    names.Add(n);
                }
            }

            return names.Count == 0 ? HoverLoc.Empty : string.Join(", ", names.Distinct());
        }
    }
}
