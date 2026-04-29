import math
import time
from uuid import UUID

import structlog

from optimizer.models.request import BinCheckRequest, PackageInput, TruckDimensions
from optimizer.models.response import BinCheckResponse, RejectedPackage

logger = structlog.get_logger(__name__)

# All 6 axis-aligned orientations as index permutations of (length, width, height)
_AXIS_ORIENTATIONS = [
    (0, 1, 2), (0, 2, 1), (1, 0, 2), (1, 2, 0), (2, 0, 1), (2, 1, 0),
]
_DIM_LABELS = ("l", "w", "h")


class BinPackingSolver:
    def check(self, request: BinCheckRequest) -> BinCheckResponse:
        truck = request.truck
        packages = request.packages
        now = request.current_time_unix or int(time.time())
        truck_volume = truck.length_m * truck.width_m * truck.height_m

        # Step 1: Per-package individual feasibility (6 orientations + optional tilt)
        axis_ok: dict[UUID, bool] = {}
        tilt_ok: dict[UUID, bool] = {}
        tilt_used: dict[UUID, bool] = {}

        for pkg in packages:
            fits, _ = self._fits_axis_aligned(truck, pkg)
            axis_ok[pkg.package_id] = fits
            if not fits and pkg.can_tilt:
                fits_t, _ = self._fits_with_tilt(truck, pkg)
                tilt_ok[pkg.package_id] = fits_t
                tilt_used[pkg.package_id] = fits_t
            else:
                tilt_ok[pkg.package_id] = False
                tilt_used[pkg.package_id] = False

        impossible = [p for p in packages if not axis_ok[p.package_id] and not tilt_ok[p.package_id]]
        candidates = [p for p in packages if axis_ok[p.package_id] or tilt_ok[p.package_id]]

        # Step 2: Combined feasibility check
        cand_volume = sum(p.length_m * p.width_m * p.height_m for p in candidates)
        cand_weight = sum(p.weight_kg for p in candidates)
        all_fit = (
            not impossible
            and cand_volume <= truck_volume
            and cand_weight <= truck.max_weight_kg
        )

        # Step 3: Accept all or select best subset via priority scoring
        accepted_ids: list[UUID] = []
        rejected: list[RejectedPackage] = []

        for pkg in impossible:
            rejected.append(RejectedPackage(
                package_id=pkg.package_id,
                reason="dimension_too_large",
                priority_score=0.0,
            ))

        if all_fit:
            accepted_ids = [p.package_id for p in candidates]
        else:
            max_value = max((p.value for p in candidates), default=1.0) or 1.0
            max_weight_c = max((p.weight_kg for p in candidates), default=1.0) or 1.0

            scored = sorted(
                candidates,
                key=lambda p: self._priority_score(p, max_value, max_weight_c, now),
                reverse=True,
            )

            running_vol = 0.0
            running_wt = 0.0
            accepted_set: set[UUID] = set()

            for pkg in scored:
                pkg_vol = pkg.length_m * pkg.width_m * pkg.height_m
                if (
                    running_wt + pkg.weight_kg <= truck.max_weight_kg
                    and running_vol + pkg_vol <= truck_volume
                ):
                    accepted_set.add(pkg.package_id)
                    running_wt += pkg.weight_kg
                    running_vol += pkg_vol
                else:
                    reason = (
                        "exceeds_weight"
                        if running_wt + pkg.weight_kg > truck.max_weight_kg
                        else "exceeds_volume"
                    )
                    score = self._priority_score(pkg, max_value, max_weight_c, now)
                    rejected.append(RejectedPackage(
                        package_id=pkg.package_id,
                        reason=reason,
                        priority_score=round(score, 4),
                    ))

            accepted_ids = [p.package_id for p in packages if p.package_id in accepted_set]

        # Step 4: Accessibility check + generate loading sequence
        accepted_set_final = set(accepted_ids)
        accepted_pkgs = [p for p in packages if p.package_id in accepted_set_final]
        # Load deepest first (highest delivery_rank → front of truck)
        loading_sequence = sorted(accepted_pkgs, key=lambda p: p.delivery_rank, reverse=True)
        accessibility_ok, access_warnings = self._check_accessibility(truck, accepted_pkgs)

        # Compute utilization metrics
        total_wt = sum(p.weight_kg for p in accepted_pkgs)
        total_vol = sum(p.length_m * p.width_m * p.height_m for p in accepted_pkgs)
        requires_tilt = any(tilt_used.get(pid, False) for pid in accepted_set_final)

        return BinCheckResponse(
            all_fit=all_fit,
            accepted_package_ids=accepted_ids,
            rejected_packages=rejected,
            loading_sequence=[p.package_id for p in loading_sequence],
            requires_tilt=requires_tilt,
            requires_dispatcher_confirmation=requires_tilt,
            accessibility_ok=accessibility_ok,
            accessibility_warnings=access_warnings,
            total_weight_kg=round(total_wt, 2),
            weight_utilization_pct=round(total_wt / truck.max_weight_kg * 100, 1),
            volume_utilization_pct=round(total_vol / truck_volume * 100, 1),
        )

    def _fits_axis_aligned(self, truck: TruckDimensions, pkg: PackageInput) -> tuple[bool, str | None]:
        dims = (pkg.length_m, pkg.width_m, pkg.height_m)
        t = (truck.length_m, truck.width_m, truck.height_m)
        for i, j, k in _AXIS_ORIENTATIONS:
            if dims[i] <= t[0] and dims[j] <= t[1] and dims[k] <= t[2]:
                return True, f"{_DIM_LABELS[i]}{_DIM_LABELS[j]}{_DIM_LABELS[k]}"
        return False, None

    def _fits_with_tilt(self, truck: TruckDimensions, pkg: PackageInput) -> tuple[bool, str | None]:
        longest = max(pkg.length_m, pkg.width_m, pkg.height_m)
        L, W, H = truck.length_m, truck.width_m, truck.height_m
        if longest <= math.sqrt(L**2 + W**2):
            return True, "diagonal_floor"
        if longest <= math.sqrt(L**2 + W**2 + H**2):
            return True, "diagonal_3d"
        return False, None

    def _priority_score(
        self,
        pkg: PackageInput,
        max_value: float,
        max_weight: float,
        now: int,
    ) -> float:
        alpha, beta, gamma, delta = 0.4, 0.3, 0.2, 0.1
        sla_window = 86400  # 24h default window

        value_s = pkg.value / max_value if max_value > 0 else 0.0

        waited = (now - pkg.received_at_unix) if pkg.received_at_unix else 0
        sla_s = max(0.0, 1.0 - waited / sla_window)

        # Lighter = higher score (preserves capacity for other packages)
        weight_s = 1.0 - (pkg.weight_kg / max_weight) if max_weight > 0 else 0.0

        deadline_s = 0.0
        if pkg.hard_deadline_unix:
            remaining_h = (pkg.hard_deadline_unix - now) / 3600
            if remaining_h > 0:
                deadline_s = min(1.0, 1.0 / max(remaining_h, 0.1))

        return alpha * value_s + beta * sla_s + gamma * weight_s + delta * deadline_s

    def _check_accessibility(
        self, truck: TruckDimensions, packages: list[PackageInput]
    ) -> tuple[bool, list[str]]:
        if len(packages) <= 1:
            return True, []

        warnings: list[str] = []
        truck_cross = truck.width_m * truck.height_m

        # Warn on duplicate delivery ranks — loading order is ambiguous
        ranks = [p.delivery_rank for p in packages]
        if len(ranks) != len(set(ranks)):
            warnings.append(
                "Duplicate delivery_rank values detected — "
                "loading order is ambiguous for tied packages"
            )

        # Loading order: highest delivery_rank goes deepest (front of truck)
        by_loading = sorted(packages, key=lambda p: p.delivery_rank, reverse=True)

        # For each deep package, check if its cross-section fills so much of the truck
        # that shallower packages cannot fit beside it or in front of it
        for i, deep_pkg in enumerate(by_loading[:-1]):
            deep_cross = (
                min(deep_pkg.width_m, truck.width_m)
                * min(deep_pkg.height_m, truck.height_m)
            )
            if deep_cross / truck_cross <= 0.85:
                continue

            # Deep package fills >85% of truck cross-section — packages in front may be blocked
            shallower = by_loading[i + 1:]
            for shallow in shallower:
                shallow_cross = (
                    min(shallow.width_m, truck.width_m)
                    * min(shallow.height_m, truck.height_m)
                )
                remaining_cross = truck_cross - deep_cross
                if shallow_cross > remaining_cross * 1.1:
                    warnings.append(
                        f"Package delivery_rank={shallow.delivery_rank} "
                        f"({shallow.width_m}m×{shallow.height_m}m) "
                        f"may be blocked by delivery_rank={deep_pkg.delivery_rank} "
                        f"({deep_cross / truck_cross:.0%} of truck cross-section occupied)"
                    )
                    break

        return len(warnings) == 0, warnings
