import { Popup } from 'react-leaflet'
import { setOptions } from 'leaflet'
import { useCallback, useEffect, useLayoutEffect, useRef } from 'react'
import OperationalRecordDetails from './OperationalRecordDetails.jsx'

const popupEvents = {
  // Leaflet ajusta a vista ao abrir; depois disso, atualizar a posição não recentraliza o mapa.
  add: ({ target }) => setOptions(target, { autoPan: false }),
  remove: ({ target }) => setOptions(target, { autoPan: true }),
}

function PopupDetails({ kind, record, onReady }) {
  // O primeiro cálculo nativo precede a inserção do conteúdo React no portal.
  useLayoutEffect(() => { onReady() }, [onReady])
  return <OperationalRecordDetails kind={kind} record={record} />
}

export default function MapPopup({ kind, record, selected }) {
  const popupRef = useRef(null)
  const onReady = useCallback(() => {
    const popup = popupRef.current
    if (!popup) return
    setOptions(popup, { autoPan: true })
    popup.update()
    setOptions(popup, { autoPan: false })
  }, [])
  useEffect(() => {
    if (!selected) popupRef.current?.close()
  }, [selected])

  return (
    <Popup ref={popupRef} autoPan autoPanPaddingTopLeft={[5, 80]} eventHandlers={popupEvents}
      className="operational-popup" minWidth={210} maxWidth={280}>
      <PopupDetails kind={kind} record={record} onReady={onReady} />
    </Popup>
  )
}
