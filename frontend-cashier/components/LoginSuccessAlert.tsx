"use client"
import { useEffect } from 'react'
import Swal from 'sweetalert2'
import 'sweetalert2/dist/sweetalert2.min.css'

export default function LoginSuccessAlert() {
  useEffect(() => {
    try {
      const flag = sessionStorage.getItem('ktp_c_login_success')
      if (flag) {
        sessionStorage.removeItem('ktp_c_login_success')
        Swal.fire({
          icon: 'success',
          title: 'Giriş başarılı',
          text: 'Hoş geldiniz.',
          timer: 1800,
          showConfirmButton: false,
          showClass: { popup: 'swal2-show' },
          hideClass: { popup: 'swal2-hide' }
        })
      }
    } catch {}
  }, [])

  return null
}
