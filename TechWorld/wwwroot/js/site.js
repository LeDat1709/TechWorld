document.addEventListener("DOMContentLoaded", () => {
    // Initialize tooltips
    const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'))
    if (tooltipTriggerList.length > 0) {
        tooltipTriggerList.map((tooltipTriggerEl) => new window.bootstrap.Tooltip(tooltipTriggerEl))
    }

    // Initialize popovers
    const popoverTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="popover"]'))
    if (popoverTriggerList.length > 0) {
        popoverTriggerList.map((popoverTriggerEl) => new window.bootstrap.Popover(popoverTriggerEl))
    }

    // --- START: Global Sidebar & Mega Menu Functionality ---

    const sidebarToggleBtn = document.getElementById("sidebarToggleBtn")
    const globalSidebar = document.getElementById("global-sidebar")
    const sidebarOverlay = document.getElementById("sidebar-overlay")
    const categoryItems = document.querySelectorAll(".category-item")
    const megaMenuContainers = document.querySelectorAll(".mega-menu-container")
    let activeMegaMenu = null // Để theo dõi mega menu đang active

    let headerHeight = 0
    // brandLeftOffset không còn cần thiết, chúng ta sẽ dùng brandRightEdge
    let hideAllElementsTimeout // Timeout để ẩn sidebar và mega menu

    // === OVERLAY FUNCTIONS ===
    // Hàm hiển thị overlay cho toàn bộ trang
    function showPageOverlay() {
        document.body.classList.add("sidebar-visible")
    }

    // Hàm ẩn overlay cho toàn bộ trang
    function hidePageOverlay() {
        document.body.classList.remove("sidebar-visible")
    }

    // Hàm cập nhật vị trí của sidebar và mega menu
    function updateMenuPositions() {
        const header = document.querySelector(".header")
        const navbarBrand = document.querySelector(".navbar-brand") // Lấy element navbar-brand

        if (header && navbarBrand) {
            // Cần cả header và navbar-brand để tính toán
            headerHeight = header.offsetHeight
            // Lấy vị trí lề phải của navbar-brand so với viewport
            const brandRightEdge = navbarBrand.getBoundingClientRect().right

            // Áp dụng các vị trí cho Sidebar (vẫn trượt)
            if (globalSidebar) {
                globalSidebar.style.top = `${headerHeight + 20}px` // Nằm ngay dưới header
                globalSidebar.style.height = `calc(100vh - ${headerHeight}px)` // Chiếm phần còn lại của viewport

                // --- ĐIỀU CHỈNH CHỖ NÀY ĐỂ NGĂN HIỆU ỨNG TRƯỢT ---
                // Luôn cập nhật vị trí đúng của sidebar dù đang hiển thị hay không
                // Điều này giúp ngăn chặn việc trượt nếu sidebar được hiển thị nhưng vị trí cũ bị sai lệch
                if (globalSidebar.classList.contains("show")) {
                    globalSidebar.style.left = `${brandRightEdge}px`
                } else {
                    globalSidebar.style.left = `calc(${brandRightEdge}px - var(--sidebar-width))`
                }
            }

            // Áp dụng các vị trí cho Mega Menu (luôn cố định ở vị trí đích, chỉ fade in/out)
            megaMenuContainers.forEach((menu) => {
                menu.style.top = `${headerHeight + 20}px` // Nằm ngay dưới header
                menu.style.height = `calc(100vh - ${headerHeight}px)` // Chiếm phần còn lại của viewport
                // Xuất hiện bên phải của sidebar với 10px khoảng cách
                // Lấy vị trí lề phải hiện tại của globalSidebar
                const sidebarRightEdge = globalSidebar ? globalSidebar.getBoundingClientRect().right : brandRightEdge
                menu.style.left = `${sidebarRightEdge + 10}px`
            })
        } else {
            // Fallback nếu không tìm thấy các phần tử (ít xảy ra)
            headerHeight = 90
            if (globalSidebar) {
                globalSidebar.style.top = `${headerHeight}px`
                globalSidebar.style.height = `calc(100vh - ${headerHeight}px)`
                globalSidebar.style.left = `calc(0px - var(--sidebar-width))` // Luôn ẩn khi không có base
            }
            megaMenuContainers.forEach((menu) => {
                menu.style.top = `${headerHeight}px`
                menu.style.height = `calc(100vh - ${headerHeight}px)`
                menu.style.left = `calc(0px - var(--mega-menu-width))` // Luôn ẩn khi không có base
            })
        }
    }

    // Hàm hiển thị sidebar
    // isTriggeredByClick: true nếu mở bằng click (ví dụ: trên mobile), false nếu bằng hover
    function showSidebar(isTriggeredByClick = false) {
        if (!globalSidebar.classList.contains("show") || isTriggeredByClick || window.innerWidth <= 991.98) {
            updateMenuPositions()
        }

        globalSidebar.classList.add("show")

        // Hiển thị overlay cho toàn bộ trang
        showPageOverlay()

        // Chỉ hiện overlay và ngăn cuộn body nếu triggered by click hoặc trên mobile
        if (isTriggeredByClick || window.innerWidth <= 991.98) {
            sidebarOverlay.classList.add("show")
            document.body.style.overflow = "hidden"
        }

        // --- GIỮ NGUYÊN: Không tự động active hay show mega menu ở đây ---
    }

    // Hàm ẩn sidebar
    // isTriggeredByClick: true nếu đóng bằng click, false nếu bằng hover
    function hideSidebar(isTriggeredByClick = false) {
        globalSidebar.classList.remove("show")
        megaMenuContainers.forEach((menu) => menu.classList.remove("show"))
        activeMegaMenu = null
        categoryItems.forEach((item) => {
            item.classList.remove("active") // Loại bỏ active khỏi tất cả category items
        })

        // Ẩn overlay cho toàn bộ trang
        hidePageOverlay()

        // Chỉ ẩn overlay và cho phép cuộn body nếu chúng đang được kích hoạt
        if (isTriggeredByClick || sidebarOverlay.classList.contains("show")) {
            sidebarOverlay.classList.remove("show")
            document.body.style.overflow = ""
        }
        updateMenuPositions() // Luôn cập nhật vị trí sau khi ẩn để đảm bảo nó trượt ra đúng cách
    }

    // ==========================================================
    // LOGIC CHO SỰ KIỆN HOVER VÀ CLICK
    // ==========================================================

    // Tổng hợp tất cả các phần tử mà khi hover vào chúng thì menu sẽ không bị ẩn
    const allInteractiveElements = [globalSidebar, ...megaMenuContainers].filter(Boolean)
    if (sidebarToggleBtn) {
        // Thêm sidebarToggleBtn vào nếu nó tồn tại
        allInteractiveElements.push(sidebarToggleBtn)
    }

    // Khi chuột di chuyển vào bất kỳ phần tử nào trong khu vực tương tác, hủy bỏ việc ẩn menu
    allInteractiveElements.forEach((el) => {
        if (!el) return

        el.addEventListener("mouseenter", () => {
            clearTimeout(hideAllElementsTimeout) // Hủy bỏ timer ẩn menu

            // Hiển thị overlay khi hover vào các phần tử tương tác (chỉ trên desktop)
            if (window.innerWidth > 991.98) {
                showPageOverlay()
            }
        })

        el.addEventListener("mouseleave", () => {
            if (window.innerWidth > 991.98) {
                // Chỉ xử lý mouseleave trên desktop
                hideAllElementsTimeout = setTimeout(() => {
                    // Kiểm tra lại xem chuột có đang nằm trên bất kỳ phần tử tương tác nào không
                    const isHoveringAny = allInteractiveElements.some((item) => item && item.matches(":hover"))
                    if (!isHoveringAny) {
                        // Nếu chuột không còn nằm trên bất kỳ phần tử nào
                        hideSidebar(false) // Ẩn sidebar (không phải do click)
                    }
                }, 200) // 200ms delay để cho phép di chuyển chuột vào sidebar/mega menu
            }
        })
    })

    // Hành vi cho nút "Danh mục"
    if (sidebarToggleBtn) {
        // Kích hoạt sidebar trên desktop khi hover vào nút "Danh mục"
        sidebarToggleBtn.addEventListener("mouseenter", () => {
            if (window.innerWidth > 991.98) {
                // Chỉ trên desktop
                clearTimeout(hideAllElementsTimeout) // Đảm bảo không bị ẩn ngay
                // --- ĐIỀU CHỈNH CHỖ NÀY ĐỂ NGĂN HIỆU ỨNG TRƯỢT KHI SIDEBAR ĐÃ HIỂN THỊ ---
                if (!globalSidebar.classList.contains("show")) {
                    // Chỉ gọi showSidebar nếu nó chưa hiển thị
                    showSidebar(false) // Hiển thị sidebar (do hover, không phải click)
                }
            }
        })

        // Nút "Danh mục" trên mobile vẫn dùng click để toggle sidebar + overlay.
        sidebarToggleBtn.addEventListener("click", () => {
            if (window.innerWidth <= 991.98) {
                // Chỉ hoạt động trên mobile
                if (globalSidebar.classList.contains("show")) {
                    hideSidebar(true) // Ẩn sidebar (do click)
                } else {
                    showSidebar(true) // Hiển thị sidebar (do click)
                }
            }
        })
    }

    // Đóng sidebar khi click vào overlay (chỉ trên mobile)
    if (sidebarOverlay) {
        sidebarOverlay.addEventListener("click", () => {
            if (window.innerWidth <= 991.98) {
                // Chỉ hoạt động trên mobile
                hideSidebar(true) // Ẩn sidebar (do click)
            }
        })
    }

    // Đóng sidebar khi click vào page overlay
    document.body.addEventListener("click", (e) => {
        if (document.body.classList.contains("sidebar-visible")) {
            // Kiểm tra xem click có phải vào các phần tử tương tác không
            const isClickOnInteractiveElement = allInteractiveElements.some((el) => el && el.contains(e.target))

            if (!isClickOnInteractiveElement) {
                hideSidebar(true) // Ẩn sidebar khi click ra ngoài
            }
        }
    })

    // Xử lý hover/click cho các category item trong sidebar (chỉ cho phần mở mega menu)
    if (globalSidebar) {
        categoryItems.forEach((item) => {
            // Hiển thị mega menu khi hover vào category item (trên desktop)
            item.addEventListener("mouseenter", function () {
                if (window.innerWidth > 991.98) {
                    const targetMegaMenuId = this.dataset.targetMegaMenu
                    const targetMegaMenu = document.querySelector(targetMegaMenuId)

                    if (targetMegaMenu) {
                        if (activeMegaMenu && activeMegaMenu !== targetMegaMenu) {
                            activeMegaMenu.classList.remove("show")
                        }
                        targetMegaMenu.classList.add("show")
                        activeMegaMenu = targetMegaMenu
                    } else {
                        // Nếu không có mega menu cho item này, ẩn cái đang hiển thị
                        if (activeMegaMenu) {
                            activeMegaMenu.classList.remove("show")
                            activeMegaMenu = null
                        }
                    }

                    categoryItems.forEach((otherItem) => {
                        if (otherItem !== this) {
                            otherItem.classList.remove("active")
                        }
                    })
                    this.classList.add("active")
                }
            })

            // Xử lý click trên mobile để toggle mega menu (hoặc chuyển trang)
            item.addEventListener("click", function (e) {
                const targetMegaMenuId = this.dataset.targetMegaMenu
                const targetMegaMenu = document.querySelector(targetMegaMenuId)

                if (window.innerWidth <= 991.98 && targetMegaMenu) {
                    e.preventDefault() // Ngăn chặn hành vi mặc định của link trên mobile nếu có mega menu
                    if (targetMegaMenu.classList.contains("show")) {
                        targetMegaMenu.classList.remove("show")
                        this.classList.remove("active")
                        activeMegaMenu = null
                    } else {
                        megaMenuContainers.forEach((otherMegaMenu) => {
                            otherMegaMenu.classList.remove("show")
                        })
                        categoryItems.forEach((otherItem) => otherItem.classList.remove("active"))

                        targetMegaMenu.classList.add("show")
                        this.classList.add("active")
                        activeMegaMenu = targetMegaMenu
                    }
                }
            })
        })
    }

    // Thêm event listener cho TẤT CẢ các link trong mega menu để đóng menu khi click
    const megaMenuLinks = document.querySelectorAll(".mega-menu-container a")
    megaMenuLinks.forEach((link) => {
        link.addEventListener("click", () => {
            // Luôn ẩn sidebar và mega menu khi một liên kết trong mega menu được click
            // (trên cả desktop và mobile). Navigation sẽ xảy ra sau đó.
            hideSidebar(false) // Gọi hideSidebar, không cần isTriggeredByClick = true vì navigation sẽ reload trang
        })
    })

    // ==========================================================
    // KẾT THÚC LOGIC HOVER VÀ CLICK
    // ==========================================================

    // === Đảm bảo sidebar ẩn sau khi lọc sản phẩm ===
    // Xử lý trạng thái active khi tải trang (nếu có categoryId trong URL)
    const urlParams = new URLSearchParams(window.location.search)
    const selectedCategoryIdParam = urlParams.get("categoryId")
    if (selectedCategoryIdParam) {
        const currentActiveCategoryItem = document.querySelector(
            `.category-item[data-category-id="${selectedCategoryIdParam}"]`,
        )
        if (currentActiveCategoryItem) {
            // Nếu có categoryId trong URL, chỉ active item tương ứng.
            // KHÔNG GỌI showSidebar() ở đây để tránh tự động hiện lại.
            currentActiveCategoryItem.classList.add("active")
        }
    }
    // Dù có hay không có categoryId trong URL, sidebar đều phải ẩn ban đầu khi tải trang.
    hideSidebar(false) // Đảm bảo sidebar ẩn ngay khi tải trang

    // Gọi hàm updateMenuPositions khi DOMContentLoaded và khi resize cửa sổ
    updateMenuPositions() // Lần gọi đầu tiên khi trang tải
    window.addEventListener("resize", updateMenuPositions) // Gọi lại khi cửa sổ thay đổi kích thước

    // --- Các phần code JS khác của bạn vẫn giữ nguyên dưới đây ---

    // Xử lý giữ trạng thái selected cho Sort Filter
    const sortFilter = document.getElementById("sort-filter")
    if (sortFilter) {
        const urlParams = new URLSearchParams(window.location.search)
        const currentSort = urlParams.get("sortOrder")
        let found = false
        for (let i = 0; i < sortFilter.options.length; i++) {
            const optionValue = sortFilter.options[i].value
            const optionUrlParams = new URLSearchParams(optionValue.split("?")[1])
            const optionSortOrder = optionUrlParams.get("sortOrder")

            if (currentSort === optionSortOrder) {
                sortFilter.options[i].selected = true
                found = true
                break
            } else if (!currentSort && !optionSortOrder) {
                sortFilter.options[i].selected = true
                found = true
                break
            }
        }
        if (!found) {
            sortFilter.options[0].selected = true
        }
    }

    // Giữ trạng thái mở của subcategory khi tải trang (Nếu bạn còn dùng logic này cho sublist)
    const selectedCategoryId = document.querySelector(".category-item.active")?.getAttribute("data-category-id")
    if (selectedCategoryId) {
        const subList = document.getElementById(`subcategory-${selectedCategoryId}`)
        const icon = document.querySelector(`.category-item[data-category-id='${selectedCategoryId}'] i`)
        if (subList && icon) {
            subList.classList.add("active")
            icon.classList.remove("fa-chevron-down")
            icon.classList.add("fa-chevron-up")
        }
    }

    // Chức năng đồng hồ đếm ngược
    function updateAllCountdowns() {
        const countdownElements = document.querySelectorAll(".product-countdown-timer")

        countdownElements.forEach((element) => {
            const endTimeString = element.getAttribute("data-end-time")
            if (!endTimeString) {
                element.textContent = ""
                return
            }

            const endTime = new Date(endTimeString).getTime()
            const now = new Date().getTime()
            const distance = endTime - now

            if (distance < 0) {
                element.textContent = "Đã kết thúc!"
                element.style.color = "red"
            } else {
                const days = Math.floor(distance / (1000 * 60 * 60 * 24))
                const hours = Math.floor((distance % (1000 * 60 * 60 * 24)) / (1000 * 60 * 60))
                const minutes = Math.floor((distance % (1000 * 60 * 60)) / (1000 * 60))
                const seconds = Math.floor((distance % (1000 * 60)) / 1000)

                let displayString = "Còn "
                if (days > 0) {
                    displayString += `${days} ngày `
                }
                displayString += `${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`

                element.textContent = displayString
            }
        })
    }

    setInterval(updateAllCountdowns, 1000)
    updateAllCountdowns()
})
