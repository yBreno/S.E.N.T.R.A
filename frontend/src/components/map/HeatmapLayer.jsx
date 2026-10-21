import { useEffect, useRef } from 'react'
import { useMap } from 'react-leaflet'
import { loadHeatmapFactory } from './leafletHeatAdapter.js'

const options = { radius: 28, blur: 20, minOpacity: 0, maxZoom: 12,
  gradient: { 0.25: '#3498db', 0.5: '#2ecc71', 0.75: '#f1c40f', 1: '#e74c3c' } }

export default function HeatmapLayer({ points, max, onError }) {
  const map = useMap()
  const layerRef = useRef(null)
  const latest = useRef({ points, max })
  useEffect(() => {
    latest.current = { points, max }
    layerRef.current?.setOptions({ max })
    layerRef.current?.setLatLngs(points)
  }, [points, max])
  useEffect(() => {
    let cancelled = false
    let layer
    loadHeatmapFactory().then((createLayer) => {
      if (cancelled) return
      layer = createLayer(latest.current.points, { ...options, max: latest.current.max }).addTo(map)
      layerRef.current = layer
      onError('')
    }).catch((error) => { if (!cancelled) onError(error.message) })
    return () => {
      cancelled = true
      layer?.remove()
      layerRef.current = null
    }
  }, [map, onError])
  return null
}
